using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Asserts the architectural spine through the full HTTP stack: auth, RBAC, tenant isolation,
/// transactional outbox, and audit. Shares one API + Postgres container across the class; each
/// test uses a unique employee number so methods never collide on the (company, employee_no) key.
[Collection(ApiCollection.Name)]
public sealed class EmployeeSliceTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1"; // seeded
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1"; // seeded, hr_admin
    private const string CompanyA = "01900000-0000-7000-8000-0000000000c1"; // seeded
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2"; // no data

    private readonly SahaHrApiFactory _factory;
    public EmployeeSliceTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record EmployeeDto(string id, string employeeNo, string firstName, string lastName, string status);
    private sealed record Paged(List<EmployeeDto> items, string? nextCursor);

    private static string NewEmployeeNo() => $"E-{Guid.NewGuid():N}"[..12];

    private static async Task<string> TokenAsync(HttpClient client, string tenantId, string[]? permissions = null, string? userId = null)
    {
        var resp = await client.PostAsJsonAsync("/v1/dev/token", new { tenantId, userId, permissions });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
    }

    private static void Authorize(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Health_is_ok_without_auth()
    {
        var resp = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_list_returns_401()
    {
        var resp = await _factory.CreateClient().GetAsync("/v1/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Create_then_get_roundtrips_and_writes_outbox_and_audit()
    {
        var client = _factory.CreateClient();
        Authorize(client, await TokenAsync(client, TenantA, userId: SeededUser)); // perms resolved from DB
        var employeeNo = NewEmployeeNo();

        var create = await client.PostAsJsonAsync("/v1/employees", new
        {
            companyId = CompanyA, employeeNo, firstName = "Ada", lastName = "Lim",
            workEmail = "ada@acme.example", hireDate = "2026-02-01",
        });
        var body = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"status={create.StatusCode}; body={body[..Math.Min(body.Length, 500)]}");

        var created = await create.Content.ReadFromJsonAsync<EmployeeDto>();
        Assert.NotNull(created);

        var fetched = await client.GetFromJsonAsync<EmployeeDto>($"/v1/employees/{created!.id}");
        Assert.Equal(employeeNo, fetched!.employeeNo);

        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'people.EmployeeHired'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'employee.create'") >= 1);
    }

    [Fact]
    public async Task Pii_is_encrypted_at_rest_and_decrypts_on_read()
    {
        var client = _factory.CreateClient();
        Authorize(client, await TokenAsync(client, TenantA, userId: SeededUser));

        var create = await client.PostAsJsonAsync("/v1/employees", new
        {
            companyId = CompanyA, employeeNo = NewEmployeeNo(), firstName = "Pia", lastName = "Aye",
            workEmail = (string?)null, hireDate = (string?)null,
            nationalId = "S1234567A", dateOfBirth = "1990-01-15", bankAccount = "123-456-789",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<EmployeePiiDto>();
        Assert.NotNull(created);

        // Reads round-trip (decrypted) through the API.
        var fetched = await client.GetFromJsonAsync<EmployeePiiDto>($"/v1/employees/{created!.id}");
        Assert.Equal("S1234567A", fetched!.nationalId);
        Assert.Equal("1990-01-15", fetched.dateOfBirth);
        Assert.Equal("123-456-789", fetched.bankAccount);

        // At rest: the column is populated, but the plaintext byte sequence is NOT present in storage.
        Assert.Equal(1L, await _factory.OwnerScalarAsync(
            $"SELECT count(*) FROM employee WHERE id = '{created.id}' AND national_id_enc IS NOT NULL"));
        Assert.Equal(1L, await _factory.OwnerScalarAsync(
            $"SELECT count(*) FROM employee WHERE id = '{created.id}' AND position(convert_to('S1234567A','UTF8') in national_id_enc) = 0"));

        // The (unencrypted) audit_log must not leak the plaintext NRIC.
        Assert.Equal(0L, await _factory.OwnerScalarAsync(
            "SELECT count(*) FROM audit_log WHERE after::text LIKE '%S1234567A%'"));
    }

    private sealed record EmployeePiiDto(string id, string employeeNo, string? nationalId, string? dateOfBirth, string? bankAccount);

    [Fact]
    public async Task Write_requires_write_permission()
    {
        var client = _factory.CreateClient();
        Authorize(client, await TokenAsync(client, TenantA, permissions: ["employee.read"]));

        var resp = await client.PostAsJsonAsync("/v1/employees", new
        {
            companyId = CompanyA, employeeNo = NewEmployeeNo(), firstName = "No", lastName = "Perm",
            workEmail = (string?)null, hireDate = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Tenant_isolation_other_tenant_sees_nothing()
    {
        var client = _factory.CreateClient();

        Authorize(client, await TokenAsync(client, TenantA, permissions: ["employee.read", "employee.write"], userId: SeededUser));
        var create = await client.PostAsJsonAsync("/v1/employees", new
        {
            companyId = CompanyA, employeeNo = NewEmployeeNo(), firstName = "Iso", lastName = "Late",
            workEmail = (string?)null, hireDate = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        Authorize(client, await TokenAsync(client, TenantB, permissions: ["employee.read"]));
        var listB = await client.GetFromJsonAsync<Paged>("/v1/employees");
        Assert.Empty(listB!.items);
    }

    [Fact]
    public async Task Search_filter_and_cursor_pagination_work()
    {
        var client = _factory.CreateClient();
        Authorize(client, await TokenAsync(client, TenantA, permissions: ["employee.read", "employee.write"], userId: SeededUser));

        // Seed a recognizable cohort with a shared, unique surname so search is deterministic.
        var tag = $"Pag{Guid.NewGuid():N}"[..10];
        for (var i = 0; i < 3; i++)
        {
            var resp = await client.PostAsJsonAsync("/v1/employees", new
            {
                companyId = CompanyA, employeeNo = NewEmployeeNo(),
                firstName = $"P{i}", lastName = tag, workEmail = (string?)null,
                hireDate = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        // search narrows to exactly the cohort
        var found = await client.GetFromJsonAsync<Paged>($"/v1/employees?search={tag}");
        Assert.Equal(3, found!.items.Count);
        Assert.All(found.items, e => Assert.Equal(tag, e.lastName));

        // limit=2 returns a page + a cursor; following it returns the remainder, no overlap
        var p1 = await client.GetFromJsonAsync<Paged>($"/v1/employees?search={tag}&limit=2");
        Assert.Equal(2, p1!.items.Count);
        Assert.NotNull(p1.nextCursor);

        var p2 = await client.GetFromJsonAsync<Paged>($"/v1/employees?search={tag}&limit=2&cursor={Uri.EscapeDataString(p1.nextCursor!)}");
        Assert.Single(p2!.items);
        Assert.Null(p2.nextCursor);
        Assert.DoesNotContain(p2.items[0].id, p1.items.Select(e => e.id));

        // status filter excludes the active cohort
        var terminated = await client.GetFromJsonAsync<Paged>($"/v1/employees?search={tag}&status=terminated");
        Assert.Empty(terminated!.items);
    }
}

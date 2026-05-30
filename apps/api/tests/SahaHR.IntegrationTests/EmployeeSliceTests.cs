using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Asserts the architectural spine through the full HTTP stack: auth, RBAC, tenant isolation,
/// transactional outbox, and audit. Shares one API + Postgres container across the class; each
/// test uses a unique employee number so methods never collide on the (company, employee_no) key.
public sealed class EmployeeSliceTests : IClassFixture<SahaHrApiFactory>
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1"; // seeded
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1"; // seeded, hr_admin
    private const string CompanyA = "01900000-0000-7000-8000-0000000000c1"; // seeded
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2"; // no data

    private readonly SahaHrApiFactory _factory;
    public EmployeeSliceTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record EmployeeDto(string id, string employeeNo, string firstName, string lastName, string status);

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
        var listB = await client.GetFromJsonAsync<List<EmployeeDto>>("/v1/employees");
        Assert.Empty(listB!);
    }
}

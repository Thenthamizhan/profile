using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Claims slice: submit → approve → reimburse lifecycle, maker≠checker, RBAC, money fidelity,
/// and the ClaimReimbursed event. Mirrors LeaveTests.
[Collection(ApiCollection.Name)]
public sealed class ClaimsTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string CompanyA = "01900000-0000-7000-8000-0000000000c1";
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2";

    private readonly SahaHrApiFactory _factory;
    public ClaimsTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record EmployeeDto(string id);
    private sealed record ClaimDto(string id, string status, decimal amount, string currency);

    private async Task<string> TokenAsync(HttpClient c, string[]? perms = null, string? userId = null)
    {
        var resp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId = TenantA, userId, permissions = perms });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
    }
    private static void Auth(HttpClient c, string t) => c.DefaultRequestHeaders.Authorization = new("Bearer", t);

    private async Task<string> NewEmployeeAsync(HttpClient c)
    {
        var e = await (await c.PostAsJsonAsync("/v1/employees", new
        {
            companyId = CompanyA, employeeNo = $"CL-{Guid.NewGuid():N}"[..10],
            firstName = "Claim", lastName = "Filer", workEmail = (string?)null, hireDate = (string?)null,
        })).Content.ReadFromJsonAsync<EmployeeDto>();
        return e!.id;
    }

    [Fact]
    public async Task Submit_approve_reimburse_lifecycle_with_money_fidelity()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var empId = await NewEmployeeAsync(c);

        // exact decimal amount — must survive the round-trip (numeric(18,4), not float)
        var submit = await c.PostAsJsonAsync("/v1/claims", new
        {
            employeeId = empId, category = "travel", amount = 1234.56m, currency = "SGD", description = "taxi",
        });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var claim = await submit.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("pending", claim!.status);
        Assert.Equal(1234.56m, claim.amount);

        // approve with a DIFFERENT user (maker != checker)
        Auth(c, await TokenAsync(c, perms: ["claim.approve", "claim.read", "claim.reimburse"], userId: "01900000-0000-7000-8000-0000000000d2"));
        var approved = await (await c.PostAsync($"/v1/claims/{claim.id}/approve", null)).Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("approved", approved!.status);

        // reimburse → emits ClaimReimbursed
        var reimbursed = await (await c.PostAsync($"/v1/claims/{claim.id}/reimburse", null)).Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("reimbursed", reimbursed!.status);

        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'claims.ClaimReimbursed'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'claim.reimburse'") >= 1);
        // money stored exactly
        Assert.Equal(1, await _factory.OwnerScalarAsync($"SELECT count(*) FROM expense_claim WHERE id = '{claim.id}' AND amount = 1234.56"));
    }

    [Fact]
    public async Task Requester_cannot_approve_their_own_claim()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var empId = await NewEmployeeAsync(c);
        var claim = await (await c.PostAsJsonAsync("/v1/claims", new
        {
            employeeId = empId, category = "meals", amount = 40.00m, currency = "SGD", description = (string?)null,
        })).Content.ReadFromJsonAsync<ClaimDto>();

        var resp = await c.PostAsync($"/v1/claims/{claim!.id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Reimbursing_a_non_approved_claim_is_rejected()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var empId = await NewEmployeeAsync(c);
        var claim = await (await c.PostAsJsonAsync("/v1/claims", new
        {
            employeeId = empId, category = "equipment", amount = 500.00m, currency = "SGD", description = (string?)null,
        })).Content.ReadFromJsonAsync<ClaimDto>();

        // still pending → reimburse must 409
        var resp = await c.PostAsync($"/v1/claims/{claim!.id}/reimburse", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Submitting_requires_claim_request_permission()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, perms: ["claim.read"]));
        var resp = await c.PostAsJsonAsync("/v1/claims", new
        {
            employeeId = Guid.NewGuid(), category = "travel", amount = 10.0m, currency = "SGD", description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Claims_are_tenant_isolated()
    {
        var c = _factory.CreateClient();
        // a tenant-B reader sees none of tenant A's claims (RLS)
        var bResp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId = TenantB, permissions = new[] { "claim.read" } });
        var bTok = (await bResp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
        Auth(c, bTok);
        var list = await c.GetFromJsonAsync<List<ClaimDto>>("/v1/claims");
        Assert.Empty(list!);
    }
}

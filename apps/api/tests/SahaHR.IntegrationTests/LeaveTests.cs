using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Leave & Claims slice: submit → approve/reject state machine, RBAC, maker≠checker (the requester
/// cannot approve their own leave), and tenant isolation. Mirrors the established module pattern.
[Collection(ApiCollection.Name)]
public sealed class LeaveTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string CompanyA = "01900000-0000-7000-8000-0000000000c1";
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2";

    private readonly SahaHrApiFactory _factory;
    public LeaveTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record EmployeeDto(string id);
    private sealed record LeaveDto(string id, string status, decimal days);

    private async Task<string> TokenAsync(HttpClient c, string tenantId, string[]? perms = null, string? userId = null)
    {
        var resp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId, userId, permissions = perms });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
    }
    private static void Auth(HttpClient c, string t) => c.DefaultRequestHeaders.Authorization = new("Bearer", t);

    private async Task<string> NewEmployeeAsync(HttpClient c)
    {
        var e = await (await c.PostAsJsonAsync("/v1/employees", new
        {
            companyId = CompanyA, employeeNo = $"LV-{Guid.NewGuid():N}"[..10],
            firstName = "Leave", lastName = "Taker", workEmail = (string?)null, hireDate = (string?)null,
        })).Content.ReadFromJsonAsync<EmployeeDto>();
        return e!.id;
    }

    [Fact]
    public async Task Submit_then_approve_runs_the_state_machine_and_emits_event()
    {
        var c = _factory.CreateClient();
        // submitter holds request-only; a SEPARATE approver token approves (maker != checker)
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser)); // full perms incl employee.write to create
        var empId = await NewEmployeeAsync(c);

        var submit = await c.PostAsJsonAsync("/v1/leave-requests", new
        {
            employeeId = empId, leaveType = "annual",
            startDate = "2026-07-01", endDate = "2026-07-03", days = 3.0, reason = "holiday",
        });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var leave = await submit.Content.ReadFromJsonAsync<LeaveDto>();
        Assert.Equal("pending", leave!.status);

        // approve with a DIFFERENT user id (not the requester) → allowed
        Auth(c, await TokenAsync(c, TenantA, perms: ["leave.approve", "leave.read"], userId: "01900000-0000-7000-8000-0000000000d2"));
        var approved = await (await c.PostAsync($"/v1/leave-requests/{leave.id}/approve", null))
            .Content.ReadFromJsonAsync<LeaveDto>();
        Assert.Equal("approved", approved!.status);

        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'leave.LeaveApproved'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'leave.approve'") >= 1);
    }

    [Fact]
    public async Task Requester_cannot_approve_their_own_leave()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));
        var empId = await NewEmployeeAsync(c);
        var leave = await (await c.PostAsJsonAsync("/v1/leave-requests", new
        {
            employeeId = empId, leaveType = "sick", startDate = "2026-08-01", endDate = "2026-08-01", days = 1.0, reason = (string?)null,
        })).Content.ReadFromJsonAsync<LeaveDto>();

        // same SeededUser tries to approve their own submission → 409 (maker != checker)
        var resp = await c.PostAsync($"/v1/leave-requests/{leave!.id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Submitting_requires_leave_request_permission()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, perms: ["leave.read"])); // read only
        var resp = await c.PostAsJsonAsync("/v1/leave-requests", new
        {
            employeeId = Guid.NewGuid(), leaveType = "annual", startDate = "2026-07-01", endDate = "2026-07-02", days = 2.0, reason = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Leave_requests_are_tenant_isolated()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantB, perms: ["leave.read"]));
        var list = await c.GetFromJsonAsync<List<LeaveDto>>("/v1/leave-requests");
        Assert.Empty(list!);
    }
}

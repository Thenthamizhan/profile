using System.Net;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Time & Attendance slice: clock-in opens a shift, clock-out closes it and computes hours + emits
/// time.ShiftCompleted (the payroll seam). Covers the one-open-shift guard, FK->404, RBAC, and
/// tenant isolation. Mirrors the established module test pattern.
[Collection(ApiCollection.Name)]
public sealed class AttendanceTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string CompanyA = "01900000-0000-7000-8000-0000000000c1";
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2";

    private readonly SahaHrApiFactory _factory;
    public AttendanceTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record EmployeeDto(string id);
    private sealed record AttendanceDto(string id, string status, decimal? hours);

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
            companyId = CompanyA, employeeNo = $"TA-{Guid.NewGuid():N}"[..10],
            firstName = "Time", lastName = "Keeper", workEmail = (string?)null, hireDate = (string?)null,
        })).Content.ReadFromJsonAsync<EmployeeDto>();
        return e!.id;
    }

    [Fact]
    public async Task Clock_in_then_out_computes_hours_and_emits_event()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));
        var empId = await NewEmployeeAsync(c);

        var inResp = await c.PostAsJsonAsync("/v1/attendance/clock-in", new { employeeId = empId, notes = "morning" });
        Assert.Equal(HttpStatusCode.Created, inResp.StatusCode);
        var open = await inResp.Content.ReadFromJsonAsync<AttendanceDto>();
        Assert.Equal("open", open!.status);
        Assert.Null(open.hours);

        var outResp = await c.PostAsJsonAsync("/v1/attendance/clock-out", new { employeeId = empId, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, outResp.StatusCode);
        var done = await outResp.Content.ReadFromJsonAsync<AttendanceDto>();
        Assert.Equal("completed", done!.status);
        Assert.NotNull(done.hours);
        Assert.True(done.hours >= 0);

        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'time.ShiftCompleted'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'attendance.clock_out'") >= 1);
    }

    [Fact]
    public async Task Clock_in_twice_without_clock_out_is_conflict()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));
        var empId = await NewEmployeeAsync(c);

        var first = await c.PostAsJsonAsync("/v1/attendance/clock-in", new { employeeId = empId, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await c.PostAsJsonAsync("/v1/attendance/clock-in", new { employeeId = empId, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Clock_in_for_unknown_employee_is_404()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));

        var resp = await c.PostAsJsonAsync("/v1/attendance/clock-in", new { employeeId = Guid.NewGuid(), notes = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Clocking_requires_attendance_clock_permission()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, perms: ["attendance.read"])); // read-only
        var resp = await c.PostAsJsonAsync("/v1/attendance/clock-in", new { employeeId = Guid.NewGuid(), notes = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Attendance_is_tenant_isolated()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantB, perms: ["attendance.read"]));
        var list = await c.GetFromJsonAsync<List<AttendanceDto>>("/v1/attendance");
        Assert.Empty(list!);
    }
}

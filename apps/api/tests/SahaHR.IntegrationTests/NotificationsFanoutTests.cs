using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Event fan-out: a single recruitment.CandidateHired drives TWO independent consumers via the
/// outbox dispatcher — People auto-provisions an employee, and Notifications records entries. The
/// employee creation in turn emits people.EmployeeHired, which Notifications also consumes. So one
/// "hire" action produces a candidate-hired notification AND a welcome notification, with no module
/// calling another directly.
[Collection(ApiCollection.Name)]
public sealed class NotificationsFanoutTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string SeededJob = "01900000-0000-7000-8000-00000000f001";

    private readonly SahaHrApiFactory _factory;
    public NotificationsFanoutTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record CandidateDto(string id);
    private sealed record AppDto(string id);

    private async Task<HttpClient> AuthedAsync()
    {
        var c = _factory.CreateClient();
        var resp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId = TenantA, userId = SeededUser });
        resp.EnsureSuccessStatusCode();
        var tok = (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        return c;
    }

    [Fact]
    public async Task A_hire_fans_out_to_employee_and_notifications()
    {
        var c = await AuthedAsync();

        // fresh candidate + application
        var cand = await (await c.PostAsJsonAsync("/v1/candidates",
            new { fullName = "Fanout Cand", email = $"fc-{Guid.NewGuid():N}@x.com", source = "portal" }))
            .Content.ReadFromJsonAsync<CandidateDto>();
        var app = await (await c.PostAsJsonAsync("/v1/applications", new { jobId = SeededJob, candidateId = cand!.id, matchScore = 70.0 }))
            .Content.ReadFromJsonAsync<AppDto>();

        var before = await _factory.OwnerScalarAsync("SELECT count(*) FROM notification");

        // hire via the Kanban path (emits recruitment.CandidateHired)
        var moved = await c.PostAsJsonAsync($"/v1/applications/{app!.id}/move", new { toStage = "hired" });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        // The dispatcher fans out (async). Expect at least 2 new notifications:
        //   - recruitment.CandidateHired  (CandidateHiredNotifier)
        //   - people.EmployeeHired        (EmployeeHiredNotifier, from the auto-provisioned employee)
        var total = await _factory.PollScalarAsync("SELECT count(*) FROM notification", target: before + 2);
        Assert.True(total >= before + 2, $"expected >=2 new notifications, before={before} after={total}");

        // Both topics are represented
        Assert.True(await _factory.OwnerScalarAsync(
            "SELECT count(*) FROM notification WHERE topic = 'recruitment.CandidateHired'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync(
            "SELECT count(*) FROM notification WHERE topic = 'people.EmployeeHired'") >= 1);
    }
}

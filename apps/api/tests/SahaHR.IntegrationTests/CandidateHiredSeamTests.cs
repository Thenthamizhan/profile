using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// The §5.2 cross-context seam: recruitment.CandidateHired → People auto-provisions an employee,
/// via the background OutboxDispatcher. Verifies the choreography end-to-end AND idempotency
/// (CandidateHired is at-least-once and fires from both the Kanban move and offer-accept).
[Collection(ApiCollection.Name)]
public sealed class CandidateHiredSeamTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string SeededJob = "01900000-0000-7000-8000-00000000f001";

    private readonly SahaHrApiFactory _factory;
    public CandidateHiredSeamTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record CandidateDto(string id);
    private sealed record AppDto(string id);
    private sealed record OfferDto(string id, string status);

    private async Task<HttpClient> AuthedAsync()
    {
        var c = _factory.CreateClient();
        var resp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId = TenantA, userId = SeededUser });
        resp.EnsureSuccessStatusCode();
        var tok = (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        return c;
    }

    // The auto-created employee uses a deterministic employee_no derived from the application id:
    // "H-" + first 10 hex of the GUID (no dashes), upper-cased. Mirrors EmployeeService.
    private static string ExpectedEmployeeNo(string applicationId) =>
        "H-" + Guid.Parse(applicationId).ToString("N")[..10].ToUpperInvariant();

    private async Task<string> NewApplicationAsync(HttpClient c)
    {
        var cand = await (await c.PostAsJsonAsync("/v1/candidates",
            new { fullName = "Hire Seam", email = $"hs-{Guid.NewGuid():N}@x.com", source = "portal" }))
            .Content.ReadFromJsonAsync<CandidateDto>();
        var appResp = await c.PostAsJsonAsync("/v1/applications", new { jobId = SeededJob, candidateId = cand!.id, matchScore = 80.0 });
        Assert.Equal(HttpStatusCode.Created, appResp.StatusCode);
        return (await appResp.Content.ReadFromJsonAsync<AppDto>())!.id;
    }

    [Fact]
    public async Task Accepting_an_offer_auto_provisions_an_employee()
    {
        var c = await AuthedAsync();
        var appId = await NewApplicationAsync(c);
        var employeeNo = ExpectedEmployeeNo(appId);

        // sanity: not present yet
        Assert.Equal(0, await _factory.OwnerScalarAsync(
            $"SELECT count(*) FROM employee WHERE employee_no = '{employeeNo}'"));

        // create → send → accept the offer (accept emits CandidateHired)
        var offer = await (await c.PostAsJsonAsync($"/v1/applications/{appId}/offers", new { salary = 7000m, currency = "SGD" }))
            .Content.ReadFromJsonAsync<OfferDto>();
        await c.PostAsync($"/v1/offers/{offer!.id}/send", null);
        var accepted = await (await c.PostAsJsonAsync($"/v1/offers/{offer.id}/respond", new { decision = "accepted" }))
            .Content.ReadFromJsonAsync<OfferDto>();
        Assert.Equal("accepted", accepted!.status);

        // the background dispatcher consumes CandidateHired and provisions the employee (async)
        var count = await _factory.PollScalarAsync(
            $"SELECT count(*) FROM employee WHERE employee_no = '{employeeNo}'", target: 1);
        Assert.Equal(1, count);

        // it was attributed to the ATS source in the audit log
        Assert.True(await _factory.OwnerScalarAsync(
            "SELECT count(*) FROM audit_log WHERE action = 'employee.create' AND after->>'source' = 'ats.candidate_hired'") >= 1);
    }

    [Fact]
    public async Task Auto_provisioning_is_idempotent_under_duplicate_delivery()
    {
        var c = await AuthedAsync();
        var appId = await NewApplicationAsync(c);
        var employeeNo = ExpectedEmployeeNo(appId);

        // Move straight to hired via the Kanban path (also emits CandidateHired)
        var moved = await c.PostAsJsonAsync($"/v1/applications/{appId}/move", new { toStage = "hired" });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        // wait for the first auto-provision
        Assert.Equal(1, await _factory.PollScalarAsync(
            $"SELECT count(*) FROM employee WHERE employee_no = '{employeeNo}'", target: 1));

        // Simulate a duplicate at-least-once delivery: re-enqueue the SAME CandidateHired payload.
        // (Copy the existing outbox row for this app, unprocessed, so the dispatcher handles it again.)
        await _factory.OwnerExecAsync($@"
            INSERT INTO outbox_message (id, tenant_id, type, payload, occurred_at, processed_at)
            SELECT gen_random_uuid(), tenant_id, type, payload, now(), NULL
            FROM outbox_message
            WHERE type = 'recruitment.CandidateHired'
              AND payload::jsonb->>'applicationId' = '{appId}'
            LIMIT 1;");

        // give the dispatcher time to process the duplicate, then assert STILL exactly one employee
        await Task.Delay(6000);
        Assert.Equal(1, await _factory.OwnerScalarAsync(
            $"SELECT count(*) FROM employee WHERE employee_no = '{employeeNo}'"));
    }
}

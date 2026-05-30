using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Offers + scorecards through the full HTTP stack: offer lifecycle (create → send → accept hires the
/// candidate, emitting OfferAccepted + CandidateHired), RBAC, and the weighted-average scorecard
/// roll-up. Shares the one container + API host via the collection fixture.
[Collection(ApiCollection.Name)]
public sealed class OffersScorecardsTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string SeededJob = "01900000-0000-7000-8000-00000000f001";
    private const string SeededApp = "01900000-0000-7000-8000-00000000aa01";

    private readonly SahaHrApiFactory _factory;
    public OffersScorecardsTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record CandidateDto(string id);
    private sealed record AppDto(string id, string currentStage, string status);
    private sealed record OfferDto(string id, string status, decimal? salary, string? currency);
    private sealed record InterviewDto(string id, double? rollupScore, string? recommendation);

    private static async Task<string> TokenAsync(HttpClient c, string[]? perms = null, string? userId = null)
    {
        var resp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId = TenantA, userId, permissions = perms });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
    }

    private static void Auth(HttpClient c, string token)
        => c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// Create a fresh candidate + application (independent per test) and return the application id.
    private static async Task<string> NewApplicationAsync(HttpClient c)
    {
        var cand = await (await c.PostAsJsonAsync("/v1/candidates",
            new { fullName = "Offer Candidate", email = $"oc-{Guid.NewGuid():N}@x.com", source = "portal" }))
            .Content.ReadFromJsonAsync<CandidateDto>();
        var appResp = await c.PostAsJsonAsync("/v1/applications", new { jobId = SeededJob, candidateId = cand!.id, matchScore = 90.0 });
        Assert.Equal(HttpStatusCode.Created, appResp.StatusCode);
        return (await appResp.Content.ReadFromJsonAsync<AppDto>())!.id;
    }

    [Fact]
    public async Task Offer_lifecycle_create_send_accept_hires_candidate()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));   // all perms from DB
        var appId = await NewApplicationAsync(c);

        // create draft
        var createResp = await c.PostAsJsonAsync($"/v1/applications/{appId}/offers", new { salary = 8500.00m, currency = "SGD" });
        var createBody = await createResp.Content.ReadAsStringAsync();
        Assert.True(createResp.StatusCode == HttpStatusCode.Created, $"create status={createResp.StatusCode}; body={createBody[..Math.Min(createBody.Length, 600)]}");
        var offer = await createResp.Content.ReadFromJsonAsync<OfferDto>();
        Assert.Equal("draft", offer!.status);
        Assert.Equal(8500.00m, offer.salary);

        // send
        var sent = await (await c.PostAsync($"/v1/offers/{offer.id}/send", null)).Content.ReadFromJsonAsync<OfferDto>();
        Assert.Equal("sent", sent!.status);

        // accept → hires the candidate
        var accepted = await (await c.PostAsJsonAsync($"/v1/offers/{offer.id}/respond", new { decision = "accepted" }))
            .Content.ReadFromJsonAsync<OfferDto>();
        Assert.Equal("accepted", accepted!.status);

        // the application is now hired
        Assert.Equal(1, await _factory.OwnerScalarAsync($"SELECT count(*) FROM application WHERE id = '{appId}' AND status = 'hired'"));

        // the full event chain landed in the outbox
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'recruitment.OfferExtended'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'recruitment.OfferAccepted'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'recruitment.CandidateHired'") >= 1);

        // every transition is audited
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'offer.create'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'offer.send'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'offer.respond'") >= 1);
    }

    [Fact]
    public async Task Creating_an_offer_requires_offer_write()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, perms: ["offer.read"]));   // read only
        var resp = await c.PostAsJsonAsync($"/v1/applications/{SeededApp}/offers", new { salary = 1000m, currency = "SGD" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Sending_a_non_draft_offer_is_rejected()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var appId = await NewApplicationAsync(c);
        var offer = await (await c.PostAsJsonAsync($"/v1/applications/{appId}/offers", new { salary = 5000m, currency = "SGD" }))
            .Content.ReadFromJsonAsync<OfferDto>();

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/v1/offers/{offer!.id}/send", null)).StatusCode);
        // second send is an illegal transition → 409, not 500
        var second = await c.PostAsync($"/v1/offers/{offer.id}/send", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Scorecard_rollup_is_weighted_average()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var appId = await NewApplicationAsync(c);

        var iv = await (await c.PostAsJsonAsync($"/v1/applications/{appId}/interviews",
                new { scheduledAt = "2026-06-01T10:00:00Z", interviewers = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<InterviewDto>();
        Assert.NotNull(iv);

        // (2*4 + 1*2) / (2+1) = 10/3 = 3.33
        var scored = await (await c.PostAsJsonAsync($"/v1/interviews/{iv!.id}/scorecard", new
        {
            competencies = new[]
            {
                new { name = "Technical", weight = 2.0, score = 4 },
                new { name = "Communication", weight = 1.0, score = 2 },
            },
            recommendation = "hire",
            notes = "strong on systems design",
        })).Content.ReadFromJsonAsync<InterviewDto>();

        Assert.Equal(3.33, scored!.rollupScore!.Value, 2);
        Assert.Equal("hire", scored.recommendation);

        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM outbox_message WHERE type = 'recruitment.ScorecardSubmitted'") >= 1);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM audit_log WHERE action = 'interview.scorecard'") >= 1);
    }

    [Fact]
    public async Task Scorecard_score_out_of_range_is_rejected()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var appId = await NewApplicationAsync(c);
        var iv = await (await c.PostAsJsonAsync($"/v1/applications/{appId}/interviews",
                new { scheduledAt = (string?)null, interviewers = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<InterviewDto>();

        var resp = await c.PostAsJsonAsync($"/v1/interviews/{iv!.id}/scorecard", new
        {
            competencies = new[] { new { name = "Technical", weight = 1.0, score = 9 } },  // out of 1..5
            recommendation = (string?)null,
            notes = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Submitting_a_scorecard_requires_interview_write()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, userId: SeededUser));
        var appId = await NewApplicationAsync(c);
        var iv = await (await c.PostAsJsonAsync($"/v1/applications/{appId}/interviews",
                new { scheduledAt = (string?)null, interviewers = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<InterviewDto>();

        // re-auth read-only and attempt to submit
        Auth(c, await TokenAsync(c, perms: ["interview.read"]));
        var resp = await c.PostAsJsonAsync($"/v1/interviews/{iv!.id}/scorecard", new
        {
            competencies = new[] { new { name = "Technical", weight = 1.0, score = 4 } },
            recommendation = (string?)null,
            notes = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}

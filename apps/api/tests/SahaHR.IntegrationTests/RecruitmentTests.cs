using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// Recruitment/ATS spine: tenant isolation on jobs, RBAC on job.write, application stage-move
/// emitting ApplicationMoved (outbox), and the Kanban board projection — all through full HTTP.
public sealed class RecruitmentTests : IClassFixture<SahaHrApiFactory>
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string CompanyA = "01900000-0000-7000-8000-0000000000c1";
    private const string PipelineA = "01900000-0000-7000-8000-00000000e001";
    private const string SeededJob = "01900000-0000-7000-8000-00000000f001";
    private const string SeededApp = "01900000-0000-7000-8000-00000000aa01"; // applied stage
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2";

    private readonly SahaHrApiFactory _factory;
    public RecruitmentTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record JobDto(string id, string title, string status);
    private sealed record CandidateDto(string id, string? fullName, string? email);
    private sealed record AppDto(string id, string currentStage, string status);
    private sealed record Card(string applicationId, string candidateName, decimal? matchScore, string stage);
    private sealed record Column(string key, string name, List<Card> cards);
    private sealed record Board(string jobId, string jobTitle, List<Column> columns);

    private static async Task<string> TokenAsync(HttpClient c, string tenantId, string[]? perms = null, string? userId = null)
    {
        var resp = await c.PostAsJsonAsync("/v1/dev/token", new { tenantId, userId, permissions = perms });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DevToken>())!.accessToken;
    }

    private static void Auth(HttpClient c, string token)
        => c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Seeded_job_lists_for_tenant_A()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));
        var jobs = await c.GetFromJsonAsync<List<JobDto>>("/v1/jobs");
        Assert.Contains(jobs!, j => j.title == "Senior Backend Engineer");
    }

    [Fact]
    public async Task Jobs_are_tenant_isolated()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantB, perms: ["job.read"]));
        var jobs = await c.GetFromJsonAsync<List<JobDto>>("/v1/jobs");
        Assert.Empty(jobs!);
    }

    [Fact]
    public async Task Creating_a_job_requires_job_write()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, perms: ["job.read"])); // read only
        var resp = await c.PostAsJsonAsync("/v1/jobs", new
        {
            companyId = CompanyA, pipelineId = PipelineA, title = "Nope", location = (string?)null, employmentType = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Board_groups_applications_into_stage_columns()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));
        var board = await c.GetFromJsonAsync<Board>($"/v1/jobs/{SeededJob}/board");
        Assert.Equal("Senior Backend Engineer", board!.jobTitle);
        Assert.Equal(5, board.columns.Count); // applied/screening/interview/offer/hired
        var totalCards = board.columns.Sum(col => col.cards.Count);
        Assert.True(totalCards >= 2, $"expected >=2 seeded applications across columns, got {totalCards}");
    }

    [Fact]
    public async Task Moving_an_application_changes_stage_and_emits_event()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, userId: SeededUser));

        // create a fresh candidate + application so the move is deterministic and independent
        var candResp = await c.PostAsJsonAsync("/v1/candidates", new { fullName = "Move Me", email = "move@x.com", source = "portal" });
        Assert.Equal(HttpStatusCode.Created, candResp.StatusCode);
        var cand = await candResp.Content.ReadFromJsonAsync<CandidateDto>();

        var appResp = await c.PostAsJsonAsync("/v1/applications", new { jobId = SeededJob, candidateId = cand!.id, matchScore = 75.5 });
        Assert.Equal(HttpStatusCode.Created, appResp.StatusCode);
        var app = await appResp.Content.ReadFromJsonAsync<AppDto>();
        Assert.Equal("applied", app!.currentStage);

        var moved = await (await c.PostAsJsonAsync($"/v1/applications/{app.id}/move", new { toStage = "interview" }))
            .Content.ReadFromJsonAsync<AppDto>();
        Assert.Equal("interview", moved!.currentStage);

        // the move must have produced an ApplicationMoved outbox event
        Assert.True(await _factory.OwnerScalarAsync(
            "SELECT count(*) FROM outbox_message WHERE type = 'recruitment.ApplicationMoved'") >= 1);
    }

    [Fact]
    public async Task Moving_requires_application_move_permission()
    {
        var c = _factory.CreateClient();
        Auth(c, await TokenAsync(c, TenantA, perms: ["application.read"])); // can read, cannot move
        var resp = await c.PostAsJsonAsync($"/v1/applications/{SeededApp}/move", new { toStage = "screening" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}

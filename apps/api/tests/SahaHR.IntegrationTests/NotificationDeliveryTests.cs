using System.Net.Http.Json;

namespace SahaHR.IntegrationTests;

/// The NotificationDeliveryWorker drains notification rows pending → sent in the background.
/// Verified end-to-end: a hire fans out into pending notifications, which the worker then delivers.
[Collection(ApiCollection.Name)]
public sealed class NotificationDeliveryTests
{
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string SeededUser = "01900000-0000-7000-8000-0000000000d1";
    private const string SeededJob = "01900000-0000-7000-8000-00000000f001";

    private readonly SahaHrApiFactory _factory;
    public NotificationDeliveryTests(SahaHrApiFactory factory) => _factory = factory;

    private sealed record DevToken(string accessToken);
    private sealed record CandidateDto(string id);
    private sealed record AppDto(string id);

    [Fact]
    public async Task Pending_notifications_are_delivered_to_sent()
    {
        var c = _factory.CreateClient();
        var tok = (await (await c.PostAsJsonAsync("/v1/dev/token", new { tenantId = TenantA, userId = SeededUser }))
            .Content.ReadFromJsonAsync<DevToken>())!.accessToken;
        c.DefaultRequestHeaders.Authorization = new("Bearer", tok);

        var cand = await (await c.PostAsJsonAsync("/v1/candidates",
            new { fullName = "Delivery Cand", email = $"dc-{Guid.NewGuid():N}@x.com", source = "portal" }))
            .Content.ReadFromJsonAsync<CandidateDto>();
        var app = await (await c.PostAsJsonAsync("/v1/applications", new { jobId = SeededJob, candidateId = cand!.id, matchScore = 60.0 }))
            .Content.ReadFromJsonAsync<AppDto>();

        // hire → fans out into notifications (created 'pending' by the notifier handlers)
        await c.PostAsJsonAsync($"/v1/applications/{app!.id}/move", new { toStage = "hired" });

        // at least one notification appears (pending or already delivered)
        Assert.True(await _factory.PollScalarAsync("SELECT count(*) FROM notification", target: 1) >= 1);

        // the delivery worker drains everything to 'sent' — poll until no pending rows remain
        long pending = -1;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            pending = await _factory.OwnerScalarAsync("SELECT count(*) FROM notification WHERE status = 'pending'");
            if (pending == 0) break;
            await Task.Delay(500);
        }
        Assert.Equal(0, pending);
        Assert.True(await _factory.OwnerScalarAsync("SELECT count(*) FROM notification WHERE status = 'sent'") >= 1);
    }
}

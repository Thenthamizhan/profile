using System.Net;

namespace SahaHR.IntegrationTests;

/// Ops-hardening behaviour: per-request correlation ids (auto-assigned + inbound echo) and the
/// coarse rate limiter. ProblemDetails error shaping is exercised implicitly by the other suites
/// (any unhandled throw would surface as a 500 ProblemDetails rather than crashing the host).
[Collection(ApiCollection.Name)]
public sealed class OpsHardeningTests
{
    private readonly SahaHrApiFactory _factory;
    public OpsHardeningTests(SahaHrApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Response_carries_a_correlation_id_header()
    {
        var resp = await _factory.CreateClient().GetAsync("/health");
        Assert.True(resp.Headers.Contains("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(resp.Headers.GetValues("X-Correlation-Id").FirstOrDefault()));
    }

    [Fact]
    public async Task Inbound_correlation_id_is_echoed()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add("X-Correlation-Id", "corr-test-123");
        var resp = await client.SendAsync(req);
        Assert.Equal("corr-test-123", resp.Headers.GetValues("X-Correlation-Id").First());
    }

    [Fact]
    public async Task Exceeding_the_rate_limit_returns_429()
    {
        // The limiter reads RateLimit:PermitLimit at registration time, so override it via an env var
        // (which outranks appsettings) and build an isolated server with a tiny limit. The env var is
        // scoped to this test; the ApiCollection runs sequentially so no other server build sees it.
        Environment.SetEnvironmentVariable("RateLimit__PermitLimit", "3");
        try
        {
            var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();

            var codes = new List<int>();
            for (var i = 0; i < 6; i++)
                codes.Add((int)(await client.GetAsync("/health")).StatusCode);

            Assert.Equal(3, codes.Count(c => c == (int)HttpStatusCode.OK));
            Assert.Contains((int)HttpStatusCode.TooManyRequests, codes);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RateLimit__PermitLimit", null);
        }
    }
}

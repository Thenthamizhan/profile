using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using SahaHR.Modules.Identity;

namespace SahaHR.IntegrationTests;

/// Unit tests for the OIDC bridge: an external IdP token carries identity only, so permissions are
/// resolved from our RBAC tables and projected as `perm` claims. Dev-minted tokens (which already
/// carry `perm`) must pass through untouched so dev/test/E2E are unaffected. No DB needed — the
/// resolver is stubbed.
public class PermissionClaimsTransformationTests
{
    private sealed class StubResolver(IReadOnlyList<string> perms) : IPermissionResolver
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(perms);
        }
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    private static PermissionClaimsTransformation Sut(IPermissionResolver resolver) =>
        new(resolver, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task DevToken_with_perm_passes_through_without_resolving()
    {
        var resolver = new StubResolver(["should.not.be.used"]);
        var principal = Principal(
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("perm", "employee.read"));

        var result = await Sut(resolver).TransformAsync(principal);

        Assert.Equal(0, resolver.Calls);
        Assert.Single(result.FindAll("perm"));
        Assert.Equal("employee.read", result.FindFirst("perm")!.Value);
    }

    [Fact]
    public async Task IdpToken_without_perm_resolves_and_projects_perms()
    {
        var resolver = new StubResolver(["employee.read", "employee.write"]);
        var principal = Principal(
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("sub", Guid.NewGuid().ToString()));

        var result = await Sut(resolver).TransformAsync(principal);

        Assert.Equal(1, resolver.Calls);
        var perms = result.FindAll("perm").Select(c => c.Value).ToArray();
        Assert.Equal(["employee.read", "employee.write"], perms);
    }

    [Fact]
    public async Task Missing_tenant_or_subject_passes_through()
    {
        var resolver = new StubResolver(["employee.read"]);
        var principal = Principal(new Claim("sub", Guid.NewGuid().ToString())); // no tenant_id

        var result = await Sut(resolver).TransformAsync(principal);

        Assert.Equal(0, resolver.Calls);
        Assert.Empty(result.FindAll("perm"));
    }

    [Fact]
    public async Task Resolution_is_cached_per_tenant_user()
    {
        var resolver = new StubResolver(["employee.read"]);
        var sut = Sut(resolver);
        var tenant = Guid.NewGuid().ToString();
        var user = Guid.NewGuid().ToString();

        await sut.TransformAsync(Principal(new Claim("tenant_id", tenant), new Claim("sub", user)));
        await sut.TransformAsync(Principal(new Claim("tenant_id", tenant), new Claim("sub", user)));

        Assert.Equal(1, resolver.Calls); // second call served from the 60s cache
    }
}

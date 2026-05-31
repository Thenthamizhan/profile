using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace SahaHR.Modules.Identity;

/// Bridges external IdP tokens to our authorization model. A real OIDC access token (Keycloak, §7)
/// proves identity (`sub`, `tenant_id`) but does not carry our fine-grained `perm` claims — those
/// live in our RBAC tables. This transformation resolves them and projects them as `perm` claims so
/// the existing TenantContextMiddleware and permission checks work unchanged.
///
/// Tokens that already carry `perm` (the dev mint) pass through untouched, so dev/test/E2E flows are
/// unaffected. Results are cached briefly per (tenant, user) to avoid a DB round-trip on every
/// request. The transformation is idempotent: once `perm` is present it short-circuits.
public sealed class PermissionClaimsTransformation(IPermissionResolver resolver, IMemoryCache cache)
    : IClaimsTransformation
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true })
            return principal;
        if (principal.HasClaim(c => c.Type == "perm"))
            return principal; // dev token, or already transformed this request

        var tid = principal.FindFirst("tenant_id")?.Value;
        var uid = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(tid, out var tenantId) || !Guid.TryParse(uid, out var userId))
            return principal; // no tenant/subject — nothing to resolve against

        var perms = await cache.GetOrCreateAsync($"perm:{tenantId}:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await resolver.ResolveAsync(tenantId, userId);
        }) ?? [];

        if (perms.Count == 0)
            return principal;

        // Clone (never mutate the incoming principal) and project the resolved permissions.
        var clone = principal.Clone();
        if (clone.Identity is ClaimsIdentity identity)
            foreach (var p in perms)
                identity.AddClaim(new Claim("perm", p));
        return clone;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SahaHR.Common.Tenancy;

/// Populates the scoped ITenantContext from the authenticated principal's claims.
/// Runs after authentication, before authorization.
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var tid = user.FindFirst("tenant_id")?.Value;
            var uid = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(tid, out var tenantId) && Guid.TryParse(uid, out var userId))
            {
                var permissions = user.FindAll("perm").Select(c => c.Value);
                tenant.Establish(tenantId, userId, permissions);
            }
        }

        await _next(context);
    }
}

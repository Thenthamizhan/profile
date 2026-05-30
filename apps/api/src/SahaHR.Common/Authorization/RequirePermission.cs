using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Authorization;

/// Endpoint filter enforcing a single dot-namespaced permission (action-level authZ, §7.2).
public sealed class RequirePermissionFilter : IEndpointFilter
{
    private readonly string _permission;
    public RequirePermissionFilter(string permission) => _permission = permission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenant.IsAuthenticated)
            return Results.Problem(title: "Unauthenticated", statusCode: StatusCodes.Status401Unauthorized);
        if (!tenant.Has(_permission))
            return Results.Problem(title: "Forbidden", detail: $"Requires permission '{_permission}'.", statusCode: StatusCodes.Status403Forbidden);
        return await next(context);
    }
}

public static class RequirePermissionExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
        => builder.AddEndpointFilter(new RequirePermissionFilter(permission));
}

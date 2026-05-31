using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SahaHR.Common.Modules;

namespace SahaHR.Modules.Identity;

public sealed record DevTokenRequest(Guid TenantId, Guid? UserId, string[]? Permissions);
public sealed record DevTokenResponse(string AccessToken);

public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Single source of truth for permission resolution, shared by the dev mint and the OIDC
        // claims transformation. The transformation runs for every authenticated request but is a
        // no-op for tokens that already carry `perm` (the dev mint), so dev/test/E2E are unaffected.
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // DEV ONLY: mint a JWT so the API is exercisable without a real IdP. Production replaces this
        // with Keycloak/OIDC; the IdP token carries identity only and perms are resolved from our
        // RBAC tables by PermissionClaimsTransformation. See docs/AUTH.md.
        endpoints.MapPost("/v1/dev/token", async (
            DevTokenRequest request,
            IHostEnvironment env,
            IConfiguration config,
            IPermissionResolver resolver,
            CancellationToken ct) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            var userId = request.UserId ?? Guid.NewGuid();
            var permissions = request.Permissions is { Length: > 0 }
                ? request.Permissions
                : (await resolver.ResolveAsync(request.TenantId, userId, ct)).ToArray();

            return Results.Ok(new DevTokenResponse(MintToken(config, request.TenantId, userId, permissions)));
        });
    }

    private static string MintToken(IConfiguration config, Guid tenantId, Guid userId, string[] permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString()),
        };
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

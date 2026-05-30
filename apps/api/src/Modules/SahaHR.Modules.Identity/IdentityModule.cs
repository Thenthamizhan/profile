using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SahaHR.Common.Modules;
using SahaHR.Common.Persistence;

namespace SahaHR.Modules.Identity;

public sealed record DevTokenRequest(Guid TenantId, Guid? UserId, string[]? Permissions);
public sealed record DevTokenResponse(string AccessToken);

public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration) { }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // DEV ONLY: mint a JWT so the API is exercisable without a real IdP.
        // Production replaces this with Keycloak/OIDC; only the issuer/JWKS change (§7).
        endpoints.MapPost("/v1/dev/token", async (
            DevTokenRequest request,
            IHostEnvironment env,
            IConfiguration config,
            OwnerDataSource owner,
            CancellationToken ct) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            var userId = request.UserId ?? Guid.NewGuid();
            var permissions = request.Permissions is { Length: > 0 }
                ? request.Permissions
                : (await ResolvePermissionsAsync(owner, request.TenantId, userId, ct)).ToArray();

            return Results.Ok(new DevTokenResponse(MintToken(config, request.TenantId, userId, permissions)));
        });
    }

    private static async Task<List<string>> ResolvePermissionsAsync(OwnerDataSource owner, Guid tenantId, Guid userId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.key
            FROM user_role ur
            JOIN role_permission rp ON rp.role_id = ur.role_id
            JOIN permission p ON p.id = rp.permission_id
            WHERE ur.tenant_id = @tenant AND ur.user_id = @user
            """;
        await using var conn = await owner.Source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter("tenant", tenantId));
        cmd.Parameters.Add(new NpgsqlParameter("user", userId));

        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetString(0));
        return result;
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

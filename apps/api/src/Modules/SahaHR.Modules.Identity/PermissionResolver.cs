using Npgsql;
using SahaHR.Common.Persistence;

namespace SahaHR.Modules.Identity;

/// Resolves a user's fine-grained permission keys from the RBAC tables. Shared by the dev token
/// mint and the OIDC claims transformation so there is a single source of truth for "what can this
/// user do". Uses the owner (RLS-exempt) connection filtered explicitly by tenant + user, so it is
/// safe to call before tenant context is established (e.g. during claims transformation).
public interface IPermissionResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

public sealed class PermissionResolver(OwnerDataSource owner) : IPermissionResolver
{
    private const string Sql = """
        SELECT p.key
        FROM user_role ur
        JOIN role_permission rp ON rp.role_id = ur.role_id
        JOIN permission p ON p.id = rp.permission_id
        WHERE ur.tenant_id = @tenant AND ur.user_id = @user
        """;

    public async Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = await owner.Source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = Sql;
        cmd.Parameters.Add(new NpgsqlParameter("tenant", tenantId));
        cmd.Parameters.Add(new NpgsqlParameter("user", userId));

        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetString(0));
        return result;
    }
}

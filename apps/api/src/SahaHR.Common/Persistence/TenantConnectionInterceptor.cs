using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Persistence;

/// Sets the per-connection GUC `app.tenant_id` on every connection open so Postgres RLS scopes
/// every statement to the current tenant (§4.4). Pool-safe (re-set on each open). Fails closed:
/// with no tenant context it sets the nil UUID, which matches no rows.
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenant;
    public TenantConnectionInterceptor(ITenantContext tenant) => _tenant = tenant;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = CreateSetCommand(connection);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = CreateSetCommand(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateSetCommand(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant_id";
        p.Value = (_tenant.TenantId ?? Guid.Empty).ToString();
        cmd.Parameters.Add(p);
        return cmd;
    }
}

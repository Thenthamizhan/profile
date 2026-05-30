using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Persistence;

/// Builds the SQL that pins Postgres RLS to the current tenant via the `app.tenant_id` GUC (§4.4).
/// Two scopes are used together for pooler safety (see the two interceptors below):
///   - session  (is_local = false): set on connection open — covers the DIRECT endpoint.
///   - local    (is_local = true):  set at transaction start — survives transaction-mode poolers
///                                   (PgBouncer/Neon), which pin one backend per transaction.
/// Fails closed: with no tenant context it sets the nil UUID, which matches no rows.
internal static class TenantGuc
{
    public static DbCommand CreateSetCommand(DbConnection connection, ITenantContext tenant, bool local)
    {
        var cmd = connection.CreateCommand();
        // is_local is compile-time-known per call site, so inline it (avoids bool param mapping edge cases).
        cmd.CommandText = local
            ? "SELECT set_config('app.tenant_id', @tenant_id, true)"
            : "SELECT set_config('app.tenant_id', @tenant_id, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant_id";
        p.Value = (tenant.TenantId ?? Guid.Empty).ToString();
        cmd.Parameters.Add(p);
        return cmd;
    }
}

/// Sets the SESSION-scoped tenant GUC on every connection open. Correct on the direct endpoint,
/// where one Npgsql connection maps to one server backend for its lifetime.
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenant;
    public TenantConnectionInterceptor(ITenantContext tenant) => _tenant = tenant;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = TenantGuc.CreateSetCommand(connection, _tenant, local: false);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = TenantGuc.CreateSetCommand(connection, _tenant, local: false);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

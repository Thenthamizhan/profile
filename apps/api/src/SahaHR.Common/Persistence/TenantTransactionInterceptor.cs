using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Persistence;

/// Sets the TRANSACTION-LOCAL tenant GUC (`set_config(..., true)`) at the start of every EF-initiated
/// transaction, scoped via the transaction itself. This is the pooler-safe path: a transaction-mode
/// pooler (PgBouncer / Neon's -pooler endpoint) pins a single server backend for the whole
/// transaction, so a SET LOCAL applies to exactly the statements that run in it — unlike a session
/// SET, which may land on a different backend than the query.
///
/// EF Core wraps each SaveChanges in a transaction, so all WRITES are covered on the pooler. For
/// READS to be pooler-safe they must also run inside a transaction (tracked as DEBT-004); on the
/// direct endpoint the session interceptor already covers non-transactional reads.
public sealed class TenantTransactionInterceptor : DbTransactionInterceptor
{
    private readonly ITenantContext _tenant;
    public TenantTransactionInterceptor(ITenantContext tenant) => _tenant = tenant;

    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        using var cmd = TenantGuc.CreateSetCommand(connection, _tenant, local: true);
        cmd.Transaction = result;
        cmd.ExecuteNonQuery();
        return result;
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        await using var cmd = TenantGuc.CreateSetCommand(connection, _tenant, local: true);
        cmd.Transaction = result;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return result;
    }
}

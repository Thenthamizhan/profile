using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Eventing;

/// In-process stand-in for the future Kafka consumer group. Polls the outbox (owner connection,
/// RLS-exempt so it sees every tenant's rows), and for each unprocessed message:
///   1. resolves the registered IDomainEventHandlers for its EventType,
///   2. opens a DI scope and ESTABLISHES tenant context from the row's tenant_id — so handler DB
///      access runs as the app role under RLS, scoped to the right tenant (no JWT in the background),
///   3. invokes each handler with the raw JSON payload,
///   4. marks the row processed only if all handlers succeed; on failure it is left for retry
///      (at-least-once delivery — handlers must be idempotent).
/// Messages with no registered handler are still marked processed (drained), matching the prior
/// fire-and-forget relay behaviour. When extracted to Kafka, this loop becomes the consumer; the
/// IDomainEventHandler contract is unchanged.
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly OwnerDataSource _owner;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(OwnerDataSource owner, IServiceScopeFactory scopes, ILogger<OutboxDispatcher> logger)
    {
        _owner = owner;
        _scopes = scopes;
        _logger = logger;
    }

    private sealed record OutboxRow(Guid Id, Guid TenantId, string Type, string Payload);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var row in await ReadUnprocessedAsync(stoppingToken))
                {
                    if (await TryHandleAsync(row, stoppingToken))
                        await MarkProcessedAsync(row.Id, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Outbox pump failed; retrying."); }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<IReadOnlyList<OutboxRow>> ReadUnprocessedAsync(CancellationToken ct)
    {
        await using var conn = await _owner.Source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, tenant_id, type, payload FROM outbox_message WHERE processed_at IS NULL " +
            "ORDER BY occurred_at LIMIT 100";
        var rows = new List<OutboxRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new OutboxRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3)));
        return rows;
    }

    /// Returns true if the message was fully handled (or had no handlers) and may be marked processed.
    private async Task<bool> TryHandleAsync(OutboxRow row, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var handlers = sp.GetServices<IDomainEventHandler>().Where(h => h.EventType == row.Type).ToList();
        if (handlers.Count == 0)
            return true; // no consumer for this topic — drain it

        // Establish the row's tenant so handler DB access is RLS-scoped to the right tenant.
        // The dispatcher has no request principal, so the actor is the system, not a user.
        var tenant = sp.GetRequiredService<ITenantContext>();
        tenant.Establish(row.TenantId, Guid.Empty, Array.Empty<string>());

        try
        {
            foreach (var handler in handlers)
                await handler.HandleAsync(row.Payload, ct);
            return true;
        }
        catch (Exception ex)
        {
            // Leave processed_at NULL so the next pass retries (handlers are idempotent).
            _logger.LogError(ex, "Handler failed for outbox {Type} (msg {Id}); will retry.", row.Type, row.Id);
            return false;
        }
    }

    private async Task MarkProcessedAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _owner.Source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE outbox_message SET processed_at = now() WHERE id = $1";
        var p = cmd.CreateParameter();
        p.Value = id;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

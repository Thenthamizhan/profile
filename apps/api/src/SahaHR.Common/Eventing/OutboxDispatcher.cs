using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SahaHR.Common.Persistence;

namespace SahaHR.Common.Eventing;

/// In-process relay standing in for the future Kafka publisher. Drains the outbox using the
/// owner connection (RLS-exempt, cross-tenant) and marks messages processed. When the system
/// extracts services, the body here becomes "publish to Kafka"; the contract is unchanged.
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly OwnerDataSource _owner;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(OwnerDataSource owner, ILogger<OutboxDispatcher> logger)
    {
        _owner = owner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = await _owner.Source.OpenConnectionAsync(stoppingToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE outbox_message SET processed_at = now() WHERE processed_at IS NULL RETURNING type";
                var relayed = new List<string>();
                await using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
                {
                    while (await reader.ReadAsync(stoppingToken))
                        relayed.Add(reader.GetString(0));
                }
                if (relayed.Count > 0)
                    _logger.LogInformation("Outbox relayed {Count} event(s) in-process: {Types}", relayed.Count, string.Join(", ", relayed));
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Outbox pump failed; retrying."); }

            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

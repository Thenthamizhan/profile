using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SahaHR.Common.Persistence;

namespace SahaHR.Modules.Notifications;

/// Delivers recorded notifications: drains rows in status 'pending' → 'sent'. Stands in for the
/// future channel workers (email/SMS/push, Phase 4); for now "delivery" is the in-app status flip.
///
/// Mirrors OutboxDispatcher: a BackgroundService using the owner connection (RLS-exempt, so it sees
/// every tenant's pending rows in one pass — a delivery worker is cross-tenant infrastructure, not a
/// request). When real channels arrive, the per-row body becomes "send via channel, then mark sent
/// or failed"; the drain loop is unchanged.
public sealed class NotificationDeliveryWorker : BackgroundService
{
    private readonly OwnerDataSource _owner;
    private readonly ILogger<NotificationDeliveryWorker> _logger;

    public NotificationDeliveryWorker(OwnerDataSource owner, ILogger<NotificationDeliveryWorker> logger)
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
                // Atomically claim + "deliver" the batch. Real channels would send first, then mark;
                // here the status flip IS the delivery (in-app).
                cmd.CommandText =
                    "UPDATE notification SET status = 'sent' WHERE status = 'pending' RETURNING topic";
                var sent = new List<string>();
                await using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
                {
                    while (await reader.ReadAsync(stoppingToken))
                        sent.Add(reader.GetString(0));
                }
                if (sent.Count > 0)
                    _logger.LogInformation("Delivered {Count} notification(s): {Topics}", sent.Count, string.Join(", ", sent));
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Notification delivery pass failed; retrying."); }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

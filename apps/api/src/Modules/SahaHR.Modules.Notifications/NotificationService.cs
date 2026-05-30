using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.Notifications.Domain;

namespace SahaHR.Modules.Notifications;

/// Records notifications. Tenant comes from the established context (set by the outbox dispatcher
/// from the message's tenant_id when invoked from a handler).
public sealed class NotificationService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;

    public NotificationService(SahaHrDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task RecordAsync(string topic, string subject, string? body, string? recipient, CancellationToken ct)
    {
        _db.Set<Notification>().Add(new Notification
        {
            TenantId = _tenant.TenantId ?? throw new InvalidOperationException("Cannot record a notification without a tenant context."),
            Topic = topic,
            Channel = "inapp",
            Subject = subject,
            Body = body,
            Recipient = recipient,
            Status = "pending",
        });
        await _db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.Notifications.Domain;

/// A recorded notification. Event consumers create these (status 'pending'); a channel worker
/// delivers them later (Phase 4). For now recording IS the delivery (in-app).
public sealed class Notification : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Topic { get; set; } = default!;        // source event type
    public string Channel { get; set; } = "inapp";
    public string Subject { get; set; } = default!;
    public string? Body { get; set; }
    public string? Recipient { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notification");
        b.HasKey(x => x.Id);
    }
}

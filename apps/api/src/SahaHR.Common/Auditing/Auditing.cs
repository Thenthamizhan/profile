using System.Text.Json;
using SahaHR.Common.Domain;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Auditing;

/// Append-only audit record (§8.5). The app DB role has INSERT+SELECT only — never UPDATE/DELETE.
public sealed class AuditLog : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorType { get; set; }
    public string Action { get; set; } = default!;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IAuditWriter
{
    void Record(string action, string? entityType = null, Guid? entityId = null, object? before = null, object? after = null);
}

/// Writes the audit row into the caller's DbContext so it commits atomically with the action.
public sealed class AuditWriter : IAuditWriter
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditWriter(SahaHrDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public void Record(string action, string? entityType = null, Guid? entityId = null, object? before = null, object? after = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenant.TenantId ?? throw new InvalidOperationException("Cannot audit without a tenant context."),
            ActorId = _tenant.UserId,
            ActorType = "user",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Before = before is null ? null : JsonSerializer.Serialize(before),
            After = after is null ? null : JsonSerializer.Serialize(after),
        });
    }
}

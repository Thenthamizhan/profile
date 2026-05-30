using System.Text.Json;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Eventing;

/// Base domain event. Every state change emits one (§1.1, §3.3).
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// Stable topic-style name, e.g. "people.EmployeeHired" — mirrors the future Kafka topic.
    public abstract string EventType { get; }
}

public interface IEventBus
{
    void Enqueue(DomainEvent @event);
}

public interface IDomainEventHandler
{
    string EventType { get; }
    Task HandleAsync(string payload, CancellationToken ct);
}

/// Transactional outbox publisher: the event row is written inside the caller's DbContext
/// transaction, so it commits atomically with the state change — no event without a write (FF-6).
public sealed class OutboxEventBus : IEventBus
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;

    public OutboxEventBus(SahaHrDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public void Enqueue(DomainEvent @event)
    {
        _db.Outbox.Add(new OutboxMessage
        {
            TenantId = _tenant.TenantId ?? throw new InvalidOperationException("Cannot enqueue an event without a tenant context."),
            Type = @event.EventType,
            Payload = JsonSerializer.Serialize(@event, @event.GetType()),
            OccurredAt = @event.OccurredAt,
        });
    }
}

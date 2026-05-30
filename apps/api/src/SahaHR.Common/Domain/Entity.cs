namespace SahaHR.Common.Domain;

/// Carried by every tenant-scoped entity. Drives the global query filter and RLS.
public interface ITenantScoped
{
    Guid TenantId { get; }
}

/// Soft-deletable entity (HR data is rarely hard-deleted — retention/audit).
public interface ISoftDelete
{
    DateTimeOffset? DeletedAt { get; set; }
}

/// Base for entities with a surrogate UUIDv7 primary key (time-sortable, §6.1).
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

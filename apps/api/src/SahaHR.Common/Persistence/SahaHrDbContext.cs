using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;
using SahaHR.Common.Auditing;
using SahaHR.Common.Domain;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Persistence;

// ===================== shared-kernel entities (tenancy is cross-cutting) =====================

public sealed class Tenant : Entity
{
    public string Name { get; set; } = default!;
    public string Subdomain { get; set; } = default!;
    public string IsolationTier { get; set; } = "pooled";
    public string Plan { get; set; } = "standard";
    public string Region { get; set; } = "ap-southeast-1";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class Company : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string LegalName { get; set; } = default!;
    public string? Uen { get; set; }
    public string Country { get; set; } = "SG";
    public string BaseCurrency { get; set; } = "SGD";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// Transactional outbox message (platform-owned).
public sealed class OutboxMessage : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

// ===================== DI helpers =====================

public sealed class ModuleAssemblies
{
    public IReadOnlyList<Assembly> Assemblies { get; }
    public ModuleAssemblies(IEnumerable<Assembly> assemblies) => Assemblies = assemblies.Distinct().ToList();
}

/// Owner-role data source for trusted, RLS-exempt work (outbox relay, dev-token permission lookup).
/// Never used on the request path.
public sealed class OwnerDataSource
{
    public NpgsqlDataSource Source { get; }
    public OwnerDataSource(NpgsqlDataSource source) => Source = source;
}

// ===================== DbContext =====================

public sealed class SahaHrDbContext : DbContext
{
    private readonly ITenantContext _tenant;
    private readonly ModuleAssemblies _modules;

    public SahaHrDbContext(DbContextOptions<SahaHrDbContext> options, ITenantContext tenant, ModuleAssemblies modules)
        : base(options)
    {
        _tenant = tenant;
        _modules = modules;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfiguration(new TenantConfiguration());
        b.ApplyConfiguration(new CompanyConfiguration());
        b.ApplyConfiguration(new OutboxMessageConfiguration());
        b.ApplyConfiguration(new AuditLogConfiguration());

        foreach (var assembly in _modules.Assemblies)
            b.ApplyConfigurationsFromAssembly(assembly);

        // Global tenant filter on every ITenantScoped entity — ORM-level defense in depth; RLS is the backstop.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entity.ClrType))
            {
                typeof(SahaHrDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entity.ClrType)
                    .Invoke(this, new object[] { b });
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder b) where TEntity : class, ITenantScoped
        => b.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
}

// ===================== shared-kernel EF configurations =====================

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenant");
        b.HasKey(x => x.Id);
    }
}

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("company");
        b.HasKey(x => x.Id);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_message");
        b.HasKey(x => x.Id);
        b.Property(x => x.Payload).HasColumnType("jsonb");
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");
        b.HasKey(x => x.Id);
        b.Property(x => x.Before).HasColumnType("jsonb");
        b.Property(x => x.After).HasColumnType("jsonb");
    }
}

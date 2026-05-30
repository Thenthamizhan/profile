using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.Identity.Domain;

public sealed class UserAccount : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string Email { get; set; } = default!;
    public string Status { get; set; } = "active";
    public bool MfaEnabled { get; set; }
    public string? SsoSubject { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Role : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsSystem { get; set; }
}

/// Global catalog — intentionally NOT tenant-scoped (§6.3).
public sealed class Permission : Entity
{
    public string Key { get; set; } = default!;
}

public sealed class RolePermission : ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}

public sealed class UserRole : ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b) { b.ToTable("user_account"); b.HasKey(x => x.Id); }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b) { b.ToTable("role"); b.HasKey(x => x.Id); }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b) { b.ToTable("permission"); b.HasKey(x => x.Id); }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b) { b.ToTable("role_permission"); b.HasKey(x => new { x.RoleId, x.PermissionId }); }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b) { b.ToTable("user_role"); b.HasKey(x => new { x.UserId, x.RoleId }); }
}

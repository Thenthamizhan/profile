using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.People.Domain;

public sealed class Employee : Entity, ITenantScoped, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string EmployeeNo { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? WorkEmail { get; set; }
    public string Status { get; set; } = "active";
    public DateOnly? HireDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employee");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmployeeNo).IsRequired();
        b.HasIndex(x => new { x.CompanyId, x.EmployeeNo }).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.Leave.Domain;

public sealed class LeaveRequest : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string LeaveType { get; set; } = default!;       // annual|sick|unpaid|...
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }                       // numeric(5,2) — fractional allowed
    public string? Reason { get; set; }
    public string Status { get; set; } = "pending";          // pending|approved|rejected|cancelled
    public Guid? RequestedBy { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("leave_request");
        b.HasKey(x => x.Id);
        b.Property(x => x.Days).HasColumnType("numeric(5,2)");
    }
}

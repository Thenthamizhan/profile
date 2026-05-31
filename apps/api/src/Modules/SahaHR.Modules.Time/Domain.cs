using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.Time.Domain;

/// A single attendance shift: one clock-in, optionally closed by a clock-out which computes worked
/// hours. Tenant-scoped (FF-2). Hours is numeric, never float (FF-5 family) — it feeds payroll.
public sealed class AttendanceEntry : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTimeOffset ClockIn { get; set; }
    public DateTimeOffset? ClockOut { get; set; }
    public decimal? Hours { get; set; }                  // numeric(6,2); null while the shift is open
    public string Status { get; set; } = "open";          // open|completed
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
    public void Configure(EntityTypeBuilder<AttendanceEntry> b)
    {
        b.ToTable("attendance_entry");
        b.HasKey(x => x.Id);
        b.Property(x => x.Hours).HasColumnType("numeric(6,2)");
    }
}

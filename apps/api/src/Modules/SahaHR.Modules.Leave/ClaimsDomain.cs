using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.Leave.Domain;

/// Expense claim — the "Claims" half of the Leave & Claims context.
public sealed class ExpenseClaim : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Category { get; set; } = default!;       // travel|meals|equipment|...
    public decimal Amount { get; set; }                    // numeric(18,4) — money, never float
    public string Currency { get; set; } = "SGD";
    public string? Description { get; set; }
    public string Status { get; set; } = "pending";         // pending|approved|rejected|reimbursed
    public Guid? RequestedBy { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset? ReimbursedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ExpenseClaimConfiguration : IEntityTypeConfiguration<ExpenseClaim>
{
    public void Configure(EntityTypeBuilder<ExpenseClaim> b)
    {
        b.ToTable("expense_claim");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("numeric(18,4)");
    }
}

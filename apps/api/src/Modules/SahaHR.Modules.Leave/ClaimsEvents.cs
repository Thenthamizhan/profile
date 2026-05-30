using SahaHR.Common.Eventing;

namespace SahaHR.Modules.Leave.Events;

/// Emitted when an expense claim is submitted.
public sealed record ClaimSubmitted : DomainEvent
{
    public required Guid ClaimId { get; init; }
    public required Guid EmployeeId { get; init; }
    public required decimal Amount { get; init; }
    public override string EventType => "claims.ClaimSubmitted";
}

/// Emitted when a claim is reimbursed — the seam where Payroll/Finance would issue payment.
public sealed record ClaimReimbursed : DomainEvent
{
    public required Guid ClaimId { get; init; }
    public required Guid EmployeeId { get; init; }
    public required decimal Amount { get; init; }
    public override string EventType => "claims.ClaimReimbursed";
}

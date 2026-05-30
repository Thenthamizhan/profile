using SahaHR.Common.Eventing;

namespace SahaHR.Modules.Leave.Events;

/// Emitted when a leave request is submitted. Consumers (Notifications, future Workflow engine,
/// Payroll for unpaid leave) react via the event log.
public sealed record LeaveRequested : DomainEvent
{
    public required Guid LeaveRequestId { get; init; }
    public required Guid EmployeeId { get; init; }
    public required string LeaveType { get; init; }
    public required decimal Days { get; init; }
    public override string EventType => "leave.LeaveRequested";
}

/// Emitted when a leave request is approved — the seam where Payroll/Time would adjust balances.
public sealed record LeaveApproved : DomainEvent
{
    public required Guid LeaveRequestId { get; init; }
    public required Guid EmployeeId { get; init; }
    public required decimal Days { get; init; }
    public override string EventType => "leave.LeaveApproved";
}

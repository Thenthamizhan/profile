using SahaHR.Common.Eventing;

namespace SahaHR.Modules.Time.Events;

/// Emitted when a shift is clocked out and its hours are known. The seam where a future Payroll
/// context accumulates worked hours into a pay run (consumed by topic string, no cross-module ref).
public sealed record ShiftCompleted : DomainEvent
{
    public required Guid AttendanceId { get; init; }
    public required Guid EmployeeId { get; init; }
    public required DateOnly WorkDate { get; init; }
    public required decimal Hours { get; init; }
    public override string EventType => "time.ShiftCompleted";
}

using SahaHR.Common.Eventing;

namespace SahaHR.Modules.People.Events;

/// Emitted when an employee is created. Other contexts (Payroll, Workflow, Notifications) consume
/// this rather than calling People directly — the choreography is the event log (§5.2).
public sealed record EmployeeHired : DomainEvent
{
    public required Guid EmployeeId { get; init; }
    public required Guid CompanyId { get; init; }
    public required string EmployeeNo { get; init; }
    public override string EventType => "people.EmployeeHired";
}

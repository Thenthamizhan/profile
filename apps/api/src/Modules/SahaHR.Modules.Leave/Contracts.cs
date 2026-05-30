namespace SahaHR.Modules.Leave.Contracts;

public sealed record SubmitLeaveRequest(
    Guid EmployeeId,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days,
    string? Reason);

public sealed record LeaveResponse(
    Guid Id,
    Guid EmployeeId,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days,
    string Status,
    string? Reason);

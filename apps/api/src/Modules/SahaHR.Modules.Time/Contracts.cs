namespace SahaHR.Modules.Time.Contracts;

public sealed record ClockInRequest(Guid EmployeeId, string? Notes);
public sealed record ClockOutRequest(Guid EmployeeId, string? Notes);

public sealed record AttendanceResponse(
    Guid Id,
    Guid EmployeeId,
    DateOnly WorkDate,
    DateTimeOffset ClockIn,
    DateTimeOffset? ClockOut,
    decimal? Hours,
    string Status,
    string? Notes);

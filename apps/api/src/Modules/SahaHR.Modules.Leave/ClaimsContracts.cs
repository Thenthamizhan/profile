namespace SahaHR.Modules.Leave.Contracts;

public sealed record SubmitClaimRequest(
    Guid EmployeeId,
    string Category,
    decimal Amount,
    string? Currency,
    string? Description);

public sealed record ClaimResponse(
    Guid Id,
    Guid EmployeeId,
    string Category,
    decimal Amount,
    string Currency,
    string Status,
    string? Description);

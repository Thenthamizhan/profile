namespace SahaHR.Modules.People.Contracts;

public sealed record CreateEmployeeRequest(
    Guid CompanyId,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? WorkEmail,
    DateOnly? HireDate);

public sealed record UpdateEmployeeRequest(
    string? FirstName,
    string? LastName,
    string? WorkEmail,
    string? Status);

public sealed record EmployeeResponse(
    Guid Id,
    Guid CompanyId,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? WorkEmail,
    string Status,
    DateOnly? HireDate);

/// Cursor-paginated envelope (architecture §9.2). nextCursor is null when there are no more pages.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, string? NextCursor);

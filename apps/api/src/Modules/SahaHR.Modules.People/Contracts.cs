namespace SahaHR.Modules.People.Contracts;

public sealed record CreateEmployeeRequest(
    Guid CompanyId,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? WorkEmail,
    DateOnly? HireDate,
    // Sensitive PII — encrypted at rest (§8.3). Optional.
    string? NationalId = null,
    string? DateOfBirth = null,
    string? BankAccount = null);

public sealed record UpdateEmployeeRequest(
    string? FirstName,
    string? LastName,
    string? WorkEmail,
    string? Status,
    string? NationalId = null,
    string? DateOfBirth = null,
    string? BankAccount = null);

public sealed record EmployeeResponse(
    Guid Id,
    Guid CompanyId,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? WorkEmail,
    string Status,
    DateOnly? HireDate,
    string? NationalId = null,
    string? DateOfBirth = null,
    string? BankAccount = null);

/// Cursor-paginated envelope (architecture §9.2). nextCursor is null when there are no more pages.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, string? NextCursor);

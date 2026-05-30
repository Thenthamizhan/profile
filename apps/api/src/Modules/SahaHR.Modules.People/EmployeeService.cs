using Microsoft.EntityFrameworkCore;
using SahaHR.Common.Auditing;
using SahaHR.Common.Eventing;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.People.Contracts;
using SahaHR.Modules.People.Domain;
using SahaHR.Modules.People.Events;

namespace SahaHR.Modules.People;

/// Application service for the employee lifecycle. Every write goes through SaveChanges once, so the
/// employee row, the outbox event, and the audit record all commit in a single transaction.
public sealed class EmployeeService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public EmployeeService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct)
    {
        var employee = new Employee
        {
            TenantId = _tenant.TenantId!.Value,
            CompanyId = request.CompanyId,
            EmployeeNo = request.EmployeeNo,
            FirstName = request.FirstName,
            LastName = request.LastName,
            WorkEmail = request.WorkEmail,
            HireDate = request.HireDate,
            Status = "active",
        };

        _db.Set<Employee>().Add(employee);
        _events.Enqueue(new EmployeeHired
        {
            EmployeeId = employee.Id,
            CompanyId = employee.CompanyId,
            EmployeeNo = employee.EmployeeNo,
        });
        _audit.Record("employee.create", "employee", employee.Id, after: ToResponse(employee));

        await _db.SaveChangesAsync(ct);
        return ToResponse(employee);
    }

    public async Task<EmployeeResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var employee = await _db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, ct);
        return employee is null ? null : ToResponse(employee);
    }

    /// Keyset (cursor) pagination ordered by (employee_no, id) for a stable, index-friendly scan.
    /// Optional case-insensitive search across name/no/email and exact status filter. RLS + the
    /// global tenant filter still scope every query to the current tenant.
    public async Task<PagedResponse<EmployeeResponse>> ListAsync(
        string? search, string? status, string? cursor, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);

        var query = _db.Set<Employee>().Where(e => e.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.FirstName, pattern) ||
                EF.Functions.ILike(e.LastName, pattern) ||
                EF.Functions.ILike(e.EmployeeNo, pattern) ||
                (e.WorkEmail != null && EF.Functions.ILike(e.WorkEmail, pattern)));
        }

        if (TryDecodeCursor(cursor, out var lastNo, out var lastId))
        {
            // keyset predicate: (employee_no, id) > (lastNo, lastId)
            query = query.Where(e =>
                string.Compare(e.EmployeeNo, lastNo) > 0 ||
                (e.EmployeeNo == lastNo && e.Id.CompareTo(lastId) > 0));
        }

        // fetch one extra to detect whether a further page exists
        var page = await query
            .OrderBy(e => e.EmployeeNo).ThenBy(e => e.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (page.Count > limit)
        {
            var last = page[limit - 1];
            nextCursor = EncodeCursor(last.EmployeeNo, last.Id);
            page = page.Take(limit).ToList();
        }

        return new PagedResponse<EmployeeResponse>(page.Select(ToResponse).ToList(), nextCursor);
    }

    private static string EncodeCursor(string employeeNo, Guid id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{employeeNo}{id}"));

    private static bool TryDecodeCursor(string? cursor, out string employeeNo, out Guid id)
    {
        employeeNo = "";
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('');
            if (parts.Length == 2 && Guid.TryParse(parts[1], out id))
            {
                employeeNo = parts[0];
                return true;
            }
        }
        catch { /* malformed cursor -> treat as no cursor */ }
        return false;
    }

    public async Task<EmployeeResponse?> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        var employee = await _db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, ct);
        if (employee is null) return null;

        if (request.FirstName is not null) employee.FirstName = request.FirstName;
        if (request.LastName is not null) employee.LastName = request.LastName;
        if (request.WorkEmail is not null) employee.WorkEmail = request.WorkEmail;
        if (request.Status is not null) employee.Status = request.Status;

        _audit.Record("employee.update", "employee", employee.Id, after: ToResponse(employee));
        await _db.SaveChangesAsync(ct);
        return ToResponse(employee);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var employee = await _db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, ct);
        if (employee is null) return false;

        employee.DeletedAt = DateTimeOffset.UtcNow;
        _audit.Record("employee.delete", "employee", employee.Id);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static EmployeeResponse ToResponse(Employee e) =>
        new(e.Id, e.CompanyId, e.EmployeeNo, e.FirstName, e.LastName, e.WorkEmail, e.Status, e.HireDate);
}

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

    public async Task<IReadOnlyList<EmployeeResponse>> ListAsync(CancellationToken ct)
    {
        var employees = await _db.Set<Employee>()
            .Where(e => e.DeletedAt == null)
            .OrderBy(e => e.EmployeeNo)
            .ToListAsync(ct);
        return employees.Select(ToResponse).ToList();
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

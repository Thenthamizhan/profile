using Microsoft.EntityFrameworkCore;
using Npgsql;
using SahaHR.Common.Auditing;
using SahaHR.Common.Eventing;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.Leave.Contracts;
using SahaHR.Modules.Leave.Domain;
using SahaHR.Modules.Leave.Events;

namespace SahaHR.Modules.Leave;

public sealed class LeaveService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public LeaveService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    public async Task<LeaveResponse?> SubmitAsync(SubmitLeaveRequest r, CancellationToken ct)
    {
        if (r.EndDate < r.StartDate) throw new InvalidOperationException("End date cannot be before start date.");
        if (r.Days <= 0) throw new InvalidOperationException("Days must be greater than 0.");

        var leave = new LeaveRequest
        {
            TenantId = _tenant.TenantId!.Value,
            EmployeeId = r.EmployeeId,
            LeaveType = r.LeaveType,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Days = r.Days,
            Reason = r.Reason,
            Status = "pending",
            RequestedBy = _tenant.UserId,
        };
        _db.Set<LeaveRequest>().Add(leave);
        _events.Enqueue(new LeaveRequested { LeaveRequestId = leave.Id, EmployeeId = leave.EmployeeId, LeaveType = leave.LeaveType, Days = leave.Days });
        _audit.Record("leave.submit", "leave_request", leave.Id, after: new { leave.LeaveType, leave.Days });

        // Referential integrity is enforced by the FK leave_request.employee_id -> employee(id):
        // a non-existent employee fails the INSERT with SQLSTATE 23503, which we surface as 404
        // (null). This keeps Leave free of any People-module reference (FF-1) — the DB is the seam.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" })
        {
            return null; // employee_id has no matching employee in this tenant
        }

        return ToResponse(leave);
    }

    public async Task<IReadOnlyList<LeaveResponse>> ListAsync(string? status, CancellationToken ct)
    {
        var query = _db.Set<LeaveRequest>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
        var list = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        return list.Select(ToResponse).ToList();
    }

    public async Task<LeaveResponse?> ApproveAsync(Guid id, CancellationToken ct) => await DecideAsync(id, true, ct);
    public async Task<LeaveResponse?> RejectAsync(Guid id, CancellationToken ct) => await DecideAsync(id, false, ct);

    private async Task<LeaveResponse?> DecideAsync(Guid id, bool approve, CancellationToken ct)
    {
        var leave = await _db.Set<LeaveRequest>().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (leave is null) return null;
        if (leave.Status != "pending")
            throw new InvalidOperationException($"Only a pending request can be decided (current: {leave.Status}).");

        // Maker-checker (segregation of duties, AOM §8): the submitter cannot approve their own request.
        if (approve && _tenant.UserId is { } approver && leave.RequestedBy == approver)
            throw new InvalidOperationException("The requester cannot approve their own leave (maker ≠ checker).");

        leave.Status = approve ? "approved" : "rejected";
        leave.DecidedBy = _tenant.UserId;
        leave.DecidedAt = DateTimeOffset.UtcNow;

        if (approve)
            _events.Enqueue(new LeaveApproved { LeaveRequestId = leave.Id, EmployeeId = leave.EmployeeId, Days = leave.Days });
        _audit.Record(approve ? "leave.approve" : "leave.reject", "leave_request", leave.Id);

        await _db.SaveChangesAsync(ct);
        return ToResponse(leave);
    }

    private static LeaveResponse ToResponse(LeaveRequest l) =>
        new(l.Id, l.EmployeeId, l.LeaveType, l.StartDate, l.EndDate, l.Days, l.Status, l.Reason);
}

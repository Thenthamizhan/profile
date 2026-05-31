using Microsoft.EntityFrameworkCore;
using Npgsql;
using SahaHR.Common.Auditing;
using SahaHR.Common.Eventing;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.Time.Contracts;
using SahaHR.Modules.Time.Domain;
using SahaHR.Modules.Time.Events;

namespace SahaHR.Modules.Time;

public sealed class AttendanceService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public AttendanceService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    /// Opens a shift. Returns null when the employee does not exist in this tenant (FK 23503 -> 404).
    /// Throws InvalidOperationException (-> 409) when the employee already has an open shift.
    public async Task<AttendanceResponse?> ClockInAsync(ClockInRequest r, CancellationToken ct)
    {
        var alreadyOpen = await _db.Set<AttendanceEntry>()
            .AnyAsync(a => a.EmployeeId == r.EmployeeId && a.Status == "open", ct);
        if (alreadyOpen)
            throw new InvalidOperationException("Employee is already clocked in.");

        var now = DateTimeOffset.UtcNow;
        var entry = new AttendanceEntry
        {
            TenantId = _tenant.TenantId!.Value,
            EmployeeId = r.EmployeeId,
            WorkDate = DateOnly.FromDateTime(now.UtcDateTime),
            ClockIn = now,
            Status = "open",
            Notes = r.Notes,
        };
        _db.Set<AttendanceEntry>().Add(entry);
        _audit.Record("attendance.clock_in", "attendance_entry", entry.Id, after: new { entry.EmployeeId, entry.ClockIn });

        // FK attendance_entry.employee_id -> employee(id): a non-existent employee fails the INSERT
        // with SQLSTATE 23503, surfaced as 404 (null). Keeps Time free of any People reference (FF-1).
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            if (pg.SqlState == "23503") return null;                       // employee not in this tenant -> 404
            if (pg.SqlState == "23505")                                    // partial-unique race on the open shift
                throw new InvalidOperationException("Employee is already clocked in.");
            throw;
        }
        return ToResponse(entry);
    }

    /// Closes the employee's open shift and computes worked hours. Throws (-> 409) if not clocked in.
    public async Task<AttendanceResponse> ClockOutAsync(ClockOutRequest r, CancellationToken ct)
    {
        var entry = await _db.Set<AttendanceEntry>()
            .FirstOrDefaultAsync(a => a.EmployeeId == r.EmployeeId && a.Status == "open", ct)
            ?? throw new InvalidOperationException("Employee is not clocked in.");

        var now = DateTimeOffset.UtcNow;
        entry.ClockOut = now;
        entry.Hours = Math.Round((decimal)(now - entry.ClockIn).TotalHours, 2);
        entry.Status = "completed";
        if (!string.IsNullOrWhiteSpace(r.Notes))
            entry.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? r.Notes : $"{entry.Notes}; {r.Notes}";

        _events.Enqueue(new ShiftCompleted
        {
            AttendanceId = entry.Id,
            EmployeeId = entry.EmployeeId,
            WorkDate = entry.WorkDate,
            Hours = entry.Hours!.Value,
        });
        _audit.Record("attendance.clock_out", "attendance_entry", entry.Id, after: new { entry.EmployeeId, entry.Hours });

        await _db.SaveChangesAsync(ct);
        return ToResponse(entry);
    }

    public async Task<IReadOnlyList<AttendanceResponse>> ListAsync(Guid? employeeId, string? status, CancellationToken ct)
    {
        var query = _db.Set<AttendanceEntry>().AsQueryable();
        if (employeeId is { } emp) query = query.Where(a => a.EmployeeId == emp);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        var list = await query.OrderByDescending(a => a.ClockIn).ToListAsync(ct);
        return list.Select(ToResponse).ToList();
    }

    private static AttendanceResponse ToResponse(AttendanceEntry a) =>
        new(a.Id, a.EmployeeId, a.WorkDate, a.ClockIn, a.ClockOut, a.Hours, a.Status, a.Notes);
}

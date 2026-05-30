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

/// Expense-claim lifecycle: submit → approve|reject → reimburse. Same patterns as LeaveService:
/// maker != checker on approve, FK-enforced employee existence (FF-1-safe), events via the outbox.
public sealed class ClaimService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public ClaimService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    public async Task<ClaimResponse?> SubmitAsync(SubmitClaimRequest r, CancellationToken ct)
    {
        if (r.Amount <= 0) throw new InvalidOperationException("Amount must be greater than 0.");

        var claim = new ExpenseClaim
        {
            TenantId = _tenant.TenantId!.Value,
            EmployeeId = r.EmployeeId,
            Category = r.Category,
            Amount = r.Amount,
            Currency = string.IsNullOrWhiteSpace(r.Currency) ? "SGD" : r.Currency!,
            Description = r.Description,
            Status = "pending",
            RequestedBy = _tenant.UserId,
        };
        _db.Set<ExpenseClaim>().Add(claim);
        _events.Enqueue(new ClaimSubmitted { ClaimId = claim.Id, EmployeeId = claim.EmployeeId, Amount = claim.Amount });
        _audit.Record("claim.submit", "expense_claim", claim.Id, after: new { claim.Category, claim.Amount });

        // FK leave-style integrity: bad employee_id -> 23503 -> 404 (no People reference; FF-1 holds)
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" }) { return null; }

        return ToResponse(claim);
    }

    public async Task<IReadOnlyList<ClaimResponse>> ListAsync(string? status, CancellationToken ct)
    {
        var query = _db.Set<ExpenseClaim>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        var list = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return list.Select(ToResponse).ToList();
    }

    public async Task<ClaimResponse?> ApproveAsync(Guid id, CancellationToken ct) => await DecideAsync(id, true, ct);
    public async Task<ClaimResponse?> RejectAsync(Guid id, CancellationToken ct) => await DecideAsync(id, false, ct);

    private async Task<ClaimResponse?> DecideAsync(Guid id, bool approve, CancellationToken ct)
    {
        var claim = await _db.Set<ExpenseClaim>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (claim is null) return null;
        if (claim.Status != "pending")
            throw new InvalidOperationException($"Only a pending claim can be decided (current: {claim.Status}).");
        if (approve && _tenant.UserId is { } approver && claim.RequestedBy == approver)
            throw new InvalidOperationException("The requester cannot approve their own claim (maker ≠ checker).");

        claim.Status = approve ? "approved" : "rejected";
        claim.DecidedBy = _tenant.UserId;
        claim.DecidedAt = DateTimeOffset.UtcNow;
        _audit.Record(approve ? "claim.approve" : "claim.reject", "expense_claim", claim.Id);

        await _db.SaveChangesAsync(ct);
        return ToResponse(claim);
    }

    /// Approved claims are reimbursed (Finance/Payroll seam). Emits ClaimReimbursed.
    public async Task<ClaimResponse?> ReimburseAsync(Guid id, CancellationToken ct)
    {
        var claim = await _db.Set<ExpenseClaim>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (claim is null) return null;
        if (claim.Status != "approved")
            throw new InvalidOperationException($"Only an approved claim can be reimbursed (current: {claim.Status}).");

        claim.Status = "reimbursed";
        claim.ReimbursedAt = DateTimeOffset.UtcNow;
        _events.Enqueue(new ClaimReimbursed { ClaimId = claim.Id, EmployeeId = claim.EmployeeId, Amount = claim.Amount });
        _audit.Record("claim.reimburse", "expense_claim", claim.Id, after: new { claim.Amount });

        await _db.SaveChangesAsync(ct);
        return ToResponse(claim);
    }

    private static ClaimResponse ToResponse(ExpenseClaim c) =>
        new(c.Id, c.EmployeeId, c.Category, c.Amount, c.Currency, c.Status, c.Description);
}

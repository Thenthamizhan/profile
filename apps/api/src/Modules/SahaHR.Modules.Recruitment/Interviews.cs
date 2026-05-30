using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Auditing;
using SahaHR.Common.Domain;
using SahaHR.Common.Eventing;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.Recruitment.Contracts;
using SahaHR.Modules.Recruitment.Domain;
using SahaHR.Modules.Recruitment.Events;

namespace SahaHR.Modules.Recruitment;

public sealed class Interview : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public Guid[] Interviewers { get; set; } = Array.Empty<Guid>();   // uuid[]
    public string? Scorecard { get; set; }                           // jsonb
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> b)
    {
        b.ToTable("interview");
        b.HasKey(x => x.Id);
        b.Property(x => x.Scorecard).HasColumnType("jsonb");
    }
}

/// Interviews + structured scorecards. A scorecard is weighted competencies (each scored 1–5) that
/// roll up to a single weighted-average score (architecture §10.3). The rollup is computed at submit
/// time and stored in the scorecard JSON so reads are cheap and the figure is explainable.
public sealed class InterviewService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public InterviewService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    private Guid Tid => _tenant.TenantId!.Value;

    public async Task<InterviewResponse?> ScheduleAsync(Guid applicationId, ScheduleInterviewRequest r, CancellationToken ct)
    {
        if (!await _db.Set<Application>().AnyAsync(a => a.Id == applicationId, ct)) return null;

        var iv = new Interview
        {
            TenantId = Tid, ApplicationId = applicationId,
            ScheduledAt = r.ScheduledAt, Interviewers = r.Interviewers ?? Array.Empty<Guid>(),
        };
        _db.Set<Interview>().Add(iv);
        _audit.Record("interview.schedule", "interview", iv.Id);
        await _db.SaveChangesAsync(ct);
        return ToResponse(iv);
    }

    public async Task<InterviewResponse?> SubmitScorecardAsync(Guid id, SubmitScorecardRequest r, CancellationToken ct)
    {
        var iv = await _db.Set<Interview>().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (iv is null) return null;

        if (r.Competencies is null || r.Competencies.Count == 0)
            throw new InvalidOperationException("At least one competency is required.");
        if (r.Competencies.Any(c => c.Score < 1 || c.Score > 5))
            throw new InvalidOperationException("Each competency score must be between 1 and 5.");
        var totalWeight = r.Competencies.Sum(c => c.Weight);
        if (totalWeight <= 0)
            throw new InvalidOperationException("Total competency weight must be greater than 0.");

        var rollup = Math.Round(r.Competencies.Sum(c => c.Weight * c.Score) / totalWeight, 2);

        iv.Scorecard = JsonSerializer.Serialize(new
        {
            competencies = r.Competencies,
            recommendation = r.Recommendation,
            notes = r.Notes,
            rollupScore = rollup,
            submittedAt = DateTimeOffset.UtcNow,
        });
        _events.Enqueue(new ScorecardSubmitted { InterviewId = iv.Id, ApplicationId = iv.ApplicationId, RollupScore = rollup });
        _audit.Record("interview.scorecard", "interview", iv.Id, after: new { rollup });
        await _db.SaveChangesAsync(ct);
        return ToResponse(iv);
    }

    public async Task<IReadOnlyList<InterviewResponse>> ListAsync(Guid applicationId, CancellationToken ct)
    {
        var ivs = await _db.Set<Interview>()
            .Where(i => i.ApplicationId == applicationId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return ivs.Select(ToResponse).ToList();
    }

    private static InterviewResponse ToResponse(Interview iv)
    {
        double? rollup = null;
        string? recommendation = null;
        if (!string.IsNullOrWhiteSpace(iv.Scorecard))
        {
            try
            {
                using var doc = JsonDocument.Parse(iv.Scorecard);
                if (doc.RootElement.TryGetProperty("rollupScore", out var rs) && rs.ValueKind == JsonValueKind.Number)
                    rollup = rs.GetDouble();
                if (doc.RootElement.TryGetProperty("recommendation", out var rc) && rc.ValueKind == JsonValueKind.String)
                    recommendation = rc.GetString();
            }
            catch { /* malformed scorecard JSON → report no rollup rather than throw */ }
        }
        return new InterviewResponse(iv.Id, iv.ApplicationId, iv.ScheduledAt, iv.Interviewers, rollup, recommendation);
    }
}

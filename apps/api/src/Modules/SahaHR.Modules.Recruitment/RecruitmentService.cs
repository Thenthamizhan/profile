using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SahaHR.Common.Auditing;
using SahaHR.Common.Eventing;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.Recruitment.Contracts;
using SahaHR.Modules.Recruitment.Domain;
using SahaHR.Modules.Recruitment.Events;

namespace SahaHR.Modules.Recruitment;

public sealed class RecruitmentService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public RecruitmentService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    private Guid Tid => _tenant.TenantId!.Value;

    // ---- jobs ----

    public async Task<JobResponse> CreateJobAsync(CreateJobRequest r, CancellationToken ct)
    {
        var job = new Job
        {
            TenantId = Tid, CompanyId = r.CompanyId, PipelineId = r.PipelineId,
            Title = r.Title, Location = r.Location, EmploymentType = r.EmploymentType,
            Status = "open", PostedAt = DateTimeOffset.UtcNow, CreatedBy = _tenant.UserId,
        };
        _db.Set<Job>().Add(job);
        _audit.Record("job.create", "job", job.Id, after: new { job.Title });
        await _db.SaveChangesAsync(ct);
        return ToJob(job);
    }

    public async Task<IReadOnlyList<JobResponse>> ListJobsAsync(CancellationToken ct)
    {
        var jobs = await _db.Set<Job>().Where(j => j.DeletedAt == null)
            .OrderByDescending(j => j.PostedAt).ToListAsync(ct);
        return jobs.Select(ToJob).ToList();
    }

    // ---- candidates ----

    public async Task<CandidateResponse> CreateCandidateAsync(CreateCandidateRequest r, CancellationToken ct)
    {
        var c = new Candidate { TenantId = Tid, FullName = r.FullName, Email = r.Email, Source = r.Source };
        _db.Set<Candidate>().Add(c);
        _audit.Record("candidate.create", "candidate", c.Id);
        await _db.SaveChangesAsync(ct);
        return new CandidateResponse(c.Id, c.FullName, c.Email, c.Source);
    }

    // ---- applications ----

    public async Task<ApplicationResponse?> CreateApplicationAsync(CreateApplicationRequest r, CancellationToken ct)
    {
        // both job and candidate must exist within this tenant (RLS + filter guarantee scope)
        var jobExists = await _db.Set<Job>().AnyAsync(j => j.Id == r.JobId && j.DeletedAt == null, ct);
        var candExists = await _db.Set<Candidate>().AnyAsync(c => c.Id == r.CandidateId && c.DeletedAt == null, ct);
        if (!jobExists || !candExists) return null;

        var app = new Application
        {
            TenantId = Tid, JobId = r.JobId, CandidateId = r.CandidateId,
            CurrentStage = "applied", MatchScore = r.MatchScore, Status = "active",
        };
        _db.Set<Application>().Add(app);
        _audit.Record("application.create", "application", app.Id);
        await _db.SaveChangesAsync(ct);
        return ToApp(app);
    }

    public async Task<ApplicationResponse?> MoveAsync(Guid id, string toStage, CancellationToken ct)
    {
        var app = await _db.Set<Application>().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return null;

        var from = app.CurrentStage;
        if (from == toStage) return ToApp(app);

        app.CurrentStage = toStage;
        if (toStage == "hired") app.Status = "hired";
        if (toStage == "rejected") app.Status = "rejected";

        _events.Enqueue(new ApplicationMoved { ApplicationId = app.Id, JobId = app.JobId, FromStage = from, ToStage = toStage });
        if (toStage == "hired")
            _events.Enqueue(await HireEvents.BuildCandidateHiredAsync(_db, app, ct));
        _audit.Record("application.move", "application", app.Id, before: new { stage = from }, after: new { stage = toStage });

        await _db.SaveChangesAsync(ct);
        return ToApp(app);
    }

    // ---- Kanban board projection ----

    public async Task<BoardResponse?> BoardAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.Set<Job>().FirstOrDefaultAsync(j => j.Id == jobId && j.DeletedAt == null, ct);
        if (job is null) return null;

        var pipeline = await _db.Set<Pipeline>().FirstOrDefaultAsync(p => p.Id == job.PipelineId, ct);
        var stages = ParseStages(pipeline?.Stages);

        // join applications -> candidates (both tenant-scoped) for card labels
        var rows = await (
            from a in _db.Set<Application>().Where(a => a.JobId == jobId)
            join c in _db.Set<Candidate>() on a.CandidateId equals c.Id
            select new { a.Id, a.CandidateId, Name = c.FullName, a.MatchScore, a.CurrentStage }
        ).ToListAsync(ct);

        var byStage = rows.ToLookup(x => x.CurrentStage);
        var columns = stages.Select(s => new StageColumn(
            s.Key, s.Name,
            byStage[s.Key].Select(x => new KanbanCard(x.Id, x.CandidateId, x.Name ?? "(unnamed)", x.MatchScore, x.CurrentStage))
                .OrderByDescending(card => card.MatchScore).ToList()
        )).ToList();

        return new BoardResponse(job.Id, job.Title, columns);
    }

    // ---- helpers ----

    private static JobResponse ToJob(Job j) => new(j.Id, j.Title, j.Status, j.Location, j.EmploymentType, j.PipelineId);
    private static ApplicationResponse ToApp(Application a) => new(a.Id, a.JobId, a.CandidateId, a.CurrentStage, a.MatchScore, a.Status);

    private sealed record Stage(string Key, string Name);
    private static IReadOnlyList<Stage> ParseStages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return DefaultStages;
        try
        {
            var parsed = JsonSerializer.Deserialize<List<Stage>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed is { Count: > 0 } ? parsed : DefaultStages;
        }
        catch { return DefaultStages; }
    }

    private static readonly IReadOnlyList<Stage> DefaultStages =
    [
        new("applied", "Applied"), new("screening", "Screening"),
        new("interview", "Interview"), new("offer", "Offer"), new("hired", "Hired"),
    ];
}

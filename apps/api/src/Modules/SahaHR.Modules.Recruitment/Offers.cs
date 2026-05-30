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

public sealed class Offer : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public decimal? Salary { get; set; }            // numeric(18,4) — money is decimal, never float
    public string? Currency { get; set; }
    public string Status { get; set; } = "draft";    // draft|sent|accepted|declined
    public string? DocumentS3Key { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.ToTable("offer");
        b.HasKey(x => x.Id);
        b.Property(x => x.Salary).HasColumnType("numeric(18,4)");
        // snake_case convention yields "document_s3key"; the column is "document_s3_key". Explicit
        // mapping pays DEBT-001 (the latent trap flagged after the resume_s3_key fix). FF-18 verifies.
        b.Property(x => x.DocumentS3Key).HasColumnName("document_s3_key");
    }
}

/// Offer lifecycle: draft → sent → accepted|declined. Accepting an offer hires the candidate —
/// it moves the application to "hired" and emits CandidateHired, the same seam the Kanban "hired"
/// move uses (architecture §10.2: OfferAccepted → CandidateHired).
public sealed class OfferService
{
    private readonly SahaHrDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventBus _events;
    private readonly IAuditWriter _audit;

    public OfferService(SahaHrDbContext db, ITenantContext tenant, IEventBus events, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _audit = audit;
    }

    private Guid Tid => _tenant.TenantId!.Value;

    public async Task<OfferResponse?> CreateAsync(Guid applicationId, CreateOfferRequest r, CancellationToken ct)
    {
        // application must exist within this tenant (RLS + global filter scope the check)
        if (!await _db.Set<Application>().AnyAsync(a => a.Id == applicationId, ct)) return null;

        var offer = new Offer
        {
            TenantId = Tid, ApplicationId = applicationId,
            Salary = r.Salary, Currency = r.Currency, Status = "draft",
        };
        _db.Set<Offer>().Add(offer);
        _audit.Record("offer.create", "offer", offer.Id, after: new { offer.Salary, offer.Currency });
        await _db.SaveChangesAsync(ct);
        return ToResponse(offer);
    }

    public async Task<IReadOnlyList<OfferResponse>> ListAsync(Guid applicationId, CancellationToken ct)
    {
        var offers = await _db.Set<Offer>()
            .Where(o => o.ApplicationId == applicationId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return offers.Select(ToResponse).ToList();
    }

    public async Task<OfferResponse?> SendAsync(Guid id, CancellationToken ct)
    {
        var offer = await _db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null) return null;
        if (offer.Status != "draft")
            throw new InvalidOperationException($"Only a draft offer can be sent (current: {offer.Status}).");

        offer.Status = "sent";
        offer.SentAt = DateTimeOffset.UtcNow;
        _events.Enqueue(new OfferExtended { OfferId = offer.Id, ApplicationId = offer.ApplicationId });
        _audit.Record("offer.send", "offer", offer.Id);
        await _db.SaveChangesAsync(ct);
        return ToResponse(offer);
    }

    public async Task<OfferResponse?> RespondAsync(Guid id, string decision, CancellationToken ct)
    {
        if (decision is not ("accepted" or "declined"))
            throw new InvalidOperationException("decision must be 'accepted' or 'declined'.");

        var offer = await _db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null) return null;
        if (offer.Status != "sent")
            throw new InvalidOperationException($"Only a sent offer can be responded to (current: {offer.Status}).");

        offer.Status = decision;
        offer.RespondedAt = DateTimeOffset.UtcNow;

        if (decision == "accepted")
        {
            // accepting hires the candidate: move the application to hired + emit the hire events
            var app = await _db.Set<Application>().FirstOrDefaultAsync(a => a.Id == offer.ApplicationId, ct);
            if (app is not null)
            {
                var from = app.CurrentStage;
                app.CurrentStage = "hired";
                app.Status = "hired";
                _events.Enqueue(new ApplicationMoved { ApplicationId = app.Id, JobId = app.JobId, FromStage = from, ToStage = "hired" });
                _events.Enqueue(await HireEvents.BuildCandidateHiredAsync(_db, app, ct));
            }
            _events.Enqueue(new OfferAccepted { OfferId = offer.Id, ApplicationId = offer.ApplicationId });
        }
        else
        {
            _events.Enqueue(new OfferDeclined { OfferId = offer.Id, ApplicationId = offer.ApplicationId });
        }

        _audit.Record("offer.respond", "offer", offer.Id, after: new { decision });
        await _db.SaveChangesAsync(ct);
        return ToResponse(offer);
    }

    private static OfferResponse ToResponse(Offer o) =>
        new(o.Id, o.ApplicationId, o.Salary, o.Currency, o.Status, o.SentAt, o.RespondedAt);
}

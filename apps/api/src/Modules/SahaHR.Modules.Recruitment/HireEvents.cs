using Microsoft.EntityFrameworkCore;
using SahaHR.Common.Persistence;
using SahaHR.Modules.Recruitment.Domain;
using SahaHR.Modules.Recruitment.Events;

namespace SahaHR.Modules.Recruitment;

/// Builds an enriched CandidateHired event by joining the application's job (for company) and
/// candidate (for identity). Shared by the two emit sites — the Kanban "hired" move and offer-accept
/// — so the denormalized payload is consistent regardless of how the hire happened.
internal static class HireEvents
{
    public static async Task<CandidateHired> BuildCandidateHiredAsync(
        SahaHrDbContext db, Application app, CancellationToken ct)
    {
        var companyId = await db.Set<Job>()
            .Where(j => j.Id == app.JobId)
            .Select(j => j.CompanyId)
            .FirstOrDefaultAsync(ct);

        var candidate = await db.Set<Candidate>()
            .Where(c => c.Id == app.CandidateId)
            .Select(c => new { c.FullName, c.Email })
            .FirstOrDefaultAsync(ct);

        return new CandidateHired
        {
            ApplicationId = app.Id,
            CandidateId = app.CandidateId,
            JobId = app.JobId,
            CompanyId = companyId,
            CandidateName = candidate?.FullName,
            CandidateEmail = candidate?.Email,
        };
    }
}

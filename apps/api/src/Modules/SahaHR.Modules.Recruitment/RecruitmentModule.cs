using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Authorization;
using SahaHR.Common.Modules;
using SahaHR.Modules.Recruitment.Contracts;

namespace SahaHR.Modules.Recruitment;

public sealed class RecruitmentModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<RecruitmentService>();
        services.AddScoped<OfferService>();
        services.AddScoped<InterviewService>();
    }

    // Translate a domain-rule violation (illegal state transition, bad input) into 409, instead of
    // letting it surface as a 500.
    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (InvalidOperationException ex) { return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict); }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var jobs = endpoints.MapGroup("/v1/jobs").RequireAuthorization();

        jobs.MapGet("/", async (RecruitmentService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListJobsAsync(ct)))
            .RequirePermission("job.read");

        jobs.MapPost("/", async (CreateJobRequest r, RecruitmentService svc, CancellationToken ct) =>
            {
                var job = await svc.CreateJobAsync(r, ct);
                return Results.Created($"/v1/jobs/{job.Id}", job);
            })
            .RequirePermission("job.write");

        jobs.MapGet("/{id:guid}/board", async (Guid id, RecruitmentService svc, CancellationToken ct) =>
                await svc.BoardAsync(id, ct) is { } b ? Results.Ok(b) : Results.NotFound())
            .RequirePermission("application.read");

        var candidates = endpoints.MapGroup("/v1/candidates").RequireAuthorization();

        candidates.MapPost("/", async (CreateCandidateRequest r, RecruitmentService svc, CancellationToken ct) =>
            {
                var c = await svc.CreateCandidateAsync(r, ct);
                return Results.Created($"/v1/candidates/{c.Id}", c);
            })
            .RequirePermission("candidate.write");

        var apps = endpoints.MapGroup("/v1/applications").RequireAuthorization();

        apps.MapPost("/", async (CreateApplicationRequest r, RecruitmentService svc, CancellationToken ct) =>
            {
                var app = await svc.CreateApplicationAsync(r, ct);
                return app is null
                    ? Results.Problem(title: "Job or candidate not found in this tenant.", statusCode: StatusCodes.Status404NotFound)
                    : Results.Created($"/v1/applications/{app.Id}", app);
            })
            .RequirePermission("candidate.write");

        // Move a candidate across Kanban stages — the core ATS action; emits ApplicationMoved.
        apps.MapPost("/{id:guid}/move", async (Guid id, MoveApplicationRequest r, RecruitmentService svc, CancellationToken ct) =>
                await svc.MoveAsync(id, r.ToStage, ct) is { } a ? Results.Ok(a) : Results.NotFound())
            .RequirePermission("application.move");

        // ---- Offers (per application) ----
        apps.MapPost("/{id:guid}/offers", async (Guid id, CreateOfferRequest r, OfferService svc, CancellationToken ct) =>
                await svc.CreateAsync(id, r, ct) is { } o
                    ? Results.Created($"/v1/offers/{o.Id}", o)
                    : Results.Problem(title: "Application not found in this tenant.", statusCode: StatusCodes.Status404NotFound))
            .RequirePermission("offer.write");

        apps.MapGet("/{id:guid}/offers", async (Guid id, OfferService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(id, ct)))
            .RequirePermission("offer.read");

        var offers = endpoints.MapGroup("/v1/offers").RequireAuthorization();

        offers.MapPost("/{id:guid}/send", (Guid id, OfferService svc, CancellationToken ct) =>
                Guarded(async () => await svc.SendAsync(id, ct) is { } o ? Results.Ok(o) : Results.NotFound()))
            .RequirePermission("offer.write");

        offers.MapPost("/{id:guid}/respond", (Guid id, RespondOfferRequest r, OfferService svc, CancellationToken ct) =>
                Guarded(async () => await svc.RespondAsync(id, r.Decision, ct) is { } o ? Results.Ok(o) : Results.NotFound()))
            .RequirePermission("offer.write");

        // ---- Interviews + scorecards (per application) ----
        apps.MapPost("/{id:guid}/interviews", async (Guid id, ScheduleInterviewRequest r, InterviewService svc, CancellationToken ct) =>
                await svc.ScheduleAsync(id, r, ct) is { } iv
                    ? Results.Created($"/v1/interviews/{iv.Id}", iv)
                    : Results.Problem(title: "Application not found in this tenant.", statusCode: StatusCodes.Status404NotFound))
            .RequirePermission("interview.write");

        apps.MapGet("/{id:guid}/interviews", async (Guid id, InterviewService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(id, ct)))
            .RequirePermission("interview.read");

        var interviews = endpoints.MapGroup("/v1/interviews").RequireAuthorization();

        interviews.MapPost("/{id:guid}/scorecard", (Guid id, SubmitScorecardRequest r, InterviewService svc, CancellationToken ct) =>
                Guarded(async () => await svc.SubmitScorecardAsync(id, r, ct) is { } iv ? Results.Ok(iv) : Results.NotFound()))
            .RequirePermission("interview.write");
    }
}

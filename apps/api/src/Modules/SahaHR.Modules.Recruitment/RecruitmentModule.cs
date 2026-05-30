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
        => services.AddScoped<RecruitmentService>();

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
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Authorization;
using SahaHR.Common.Modules;
using SahaHR.Modules.Leave.Contracts;

namespace SahaHR.Modules.Leave;

public sealed class LeaveModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<LeaveService>();

    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (InvalidOperationException ex) { return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict); }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/leave-requests").RequireAuthorization();

        group.MapGet("/", async (string? status, LeaveService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(status, ct)))
            .RequirePermission("leave.read");

        group.MapPost("/", (SubmitLeaveRequest r, LeaveService svc, CancellationToken ct) =>
                Guarded(async () =>
                {
                    var created = await svc.SubmitAsync(r, ct);
                    return created is null
                        ? Results.Problem(title: "Employee not found in this tenant.", statusCode: StatusCodes.Status404NotFound)
                        : Results.Created($"/v1/leave-requests/{created.Id}", created);
                }))
            .RequirePermission("leave.request");

        group.MapPost("/{id:guid}/approve", (Guid id, LeaveService svc, CancellationToken ct) =>
                Guarded(async () => await svc.ApproveAsync(id, ct) is { } l ? Results.Ok(l) : Results.NotFound()))
            .RequirePermission("leave.approve");

        group.MapPost("/{id:guid}/reject", (Guid id, LeaveService svc, CancellationToken ct) =>
                Guarded(async () => await svc.RejectAsync(id, ct) is { } l ? Results.Ok(l) : Results.NotFound()))
            .RequirePermission("leave.approve");
    }
}

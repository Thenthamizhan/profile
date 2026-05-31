using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Authorization;
using SahaHR.Common.Modules;
using SahaHR.Modules.Time.Contracts;

namespace SahaHR.Modules.Time;

public sealed class TimeModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AttendanceService>();
    }

    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (InvalidOperationException ex) { return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict); }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/attendance").RequireAuthorization();

        group.MapGet("/", async (Guid? employeeId, string? status, AttendanceService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(employeeId, status, ct)))
            .RequirePermission("attendance.read");

        group.MapPost("/clock-in", (ClockInRequest r, AttendanceService svc, CancellationToken ct) =>
                Guarded(async () =>
                {
                    var created = await svc.ClockInAsync(r, ct);
                    return created is null
                        ? Results.Problem(title: "Employee not found in this tenant.", statusCode: StatusCodes.Status404NotFound)
                        : Results.Created($"/v1/attendance/{created.Id}", created);
                }))
            .RequirePermission("attendance.clock");

        group.MapPost("/clock-out", (ClockOutRequest r, AttendanceService svc, CancellationToken ct) =>
                Guarded(async () => Results.Ok(await svc.ClockOutAsync(r, ct))))
            .RequirePermission("attendance.clock");
    }
}

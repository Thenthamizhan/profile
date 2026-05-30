using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Authorization;
using SahaHR.Common.Eventing;
using SahaHR.Common.Modules;
using SahaHR.Modules.People.Contracts;

namespace SahaHR.Modules.People;

public sealed class PeopleModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<EmployeeService>();
        // Consume recruitment.CandidateHired -> auto-provision the employee (§5.2 seam).
        services.AddScoped<IDomainEventHandler, CandidateHiredHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/employees").RequireAuthorization();

        group.MapGet("/", async (
                string? search, string? status, string? cursor, int? limit,
                EmployeeService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(search, status, cursor, limit ?? 20, ct)))
            .RequirePermission("employee.read");

        group.MapGet("/{id:guid}", async (Guid id, EmployeeService svc, CancellationToken ct) =>
                await svc.GetAsync(id, ct) is { } e ? Results.Ok(e) : Results.NotFound())
            .RequirePermission("employee.read");

        group.MapPost("/", async (CreateEmployeeRequest request, EmployeeService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(request, ct);
                return Results.Created($"/v1/employees/{created.Id}", created);
            })
            .RequirePermission("employee.write");

        group.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeRequest request, EmployeeService svc, CancellationToken ct) =>
                await svc.UpdateAsync(id, request, ct) is { } e ? Results.Ok(e) : Results.NotFound())
            .RequirePermission("employee.write");

        group.MapDelete("/{id:guid}", async (Guid id, EmployeeService svc, CancellationToken ct) =>
                await svc.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission("employee.delete");
    }
}

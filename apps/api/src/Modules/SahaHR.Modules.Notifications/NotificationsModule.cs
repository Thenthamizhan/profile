using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SahaHR.Common.Eventing;
using SahaHR.Common.Modules;

namespace SahaHR.Modules.Notifications;

/// Pure consumer module — no HTTP endpoints (yet). Registers event handlers that fan out from
/// people.EmployeeHired and recruitment.CandidateHired into recorded notifications, plus a
/// background worker that delivers them (pending → sent).
public sealed class NotificationsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<IDomainEventHandler, EmployeeHiredNotifier>();
        services.AddScoped<IDomainEventHandler, CandidateHiredNotifier>();
        services.AddHostedService<NotificationDeliveryWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}

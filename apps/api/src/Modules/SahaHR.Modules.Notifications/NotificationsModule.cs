using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Eventing;
using SahaHR.Common.Modules;

namespace SahaHR.Modules.Notifications;

/// Pure consumer module — no HTTP endpoints (yet). Registers two event handlers that fan out from
/// people.EmployeeHired and recruitment.CandidateHired into recorded notifications.
public sealed class NotificationsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<IDomainEventHandler, EmployeeHiredNotifier>();
        services.AddScoped<IDomainEventHandler, CandidateHiredNotifier>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}

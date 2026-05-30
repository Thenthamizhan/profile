using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SahaHR.Common.Modules;

/// A bounded context, as an in-process module. Registers its services and maps its endpoints.
/// Extraction into a standalone service later is a lift-and-shift, not a rewrite (§5.4, §17).
public interface IModule
{
    void Register(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

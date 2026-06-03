using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TPS.Nexus.Core;

/// <summary>
/// Stub for TPS.Nexus.Core.dll — replace with actual DLL in production deployment.
/// Modules implement this interface to register services and map endpoints.
/// MapEndpoints is a NEW method added for this kanban module release.
/// All other existing modules must add an empty MapEndpoints() implementation.
/// </summary>
public interface IModuleRegistrar
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

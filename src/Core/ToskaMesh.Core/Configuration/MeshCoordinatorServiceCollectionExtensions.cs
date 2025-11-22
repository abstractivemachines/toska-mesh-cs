using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Core.Services;
using ToskaMesh.Protocols;

namespace ToskaMesh.Core.Configuration;

public static class MeshCoordinatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Orleans-backed mesh coordinator client. Requires a configured Orleans client.
    /// </summary>
    public static IServiceCollection AddMeshCoordinator(this IServiceCollection services)
    {
        services.AddSingleton<IMeshCoordinator, MeshCoordinator>();
        return services;
    }
}

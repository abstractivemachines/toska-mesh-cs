using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Protocols;
using ToskaMesh.Router.Services;

namespace ToskaMesh.Router;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the load balancer service implementation.
    /// </summary>
    public static IServiceCollection AddMeshRouter(this IServiceCollection services)
    {
        services.AddSingleton<ILoadBalancer, LoadBalancerService>();
        return services;
    }
}

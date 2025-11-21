using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Protocols;

namespace ToskaMesh.Common.ServiceDiscovery;

/// <summary>
/// Extension methods for configuring Consul client and service registry.
/// </summary>
public static class ConsulExtensions
{
    /// <summary>
    /// Adds Consul client and service registry to the service collection.
    /// </summary>
    public static IServiceCollection AddConsulServiceRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var consulConfig = new ConsulConfiguration();
        configuration.GetSection("Consul").Bind(consulConfig);

        services.AddSingleton<IConsulClient>(sp => new ConsulClient(config =>
        {
            config.Address = new Uri(consulConfig.Address);
            if (!string.IsNullOrEmpty(consulConfig.Datacenter))
            {
                config.Datacenter = consulConfig.Datacenter;
            }
            if (!string.IsNullOrEmpty(consulConfig.Token))
            {
                config.Token = consulConfig.Token;
            }
        }));

        services.AddSingleton<IServiceRegistry, ConsulServiceRegistry>();

        return services;
    }
}

/// <summary>
/// Configuration for Consul client.
/// </summary>
public class ConsulConfiguration
{
    public string Address { get; set; } = "http://localhost:8500";
    public string? Datacenter { get; set; }
    public string? Token { get; set; }
}

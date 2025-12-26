using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Core.Configuration;
using ToskaMesh.Security;
using ToskaMesh.Telemetry;

namespace ToskaMesh.Runtime.Orleans;

/// <summary>
/// Convenience methods for wiring an Orleans silo with ToskaMesh defaults.
/// </summary>
public static class MeshSiloHostingExtensions
{
    public static IHostBuilder UseMeshSilo(
        this IHostBuilder builder,
        string serviceName,
        Action<MeshStatefulOptions>? configure = null)
    {
        var options = new MeshStatefulOptions
        {
            ServiceName = serviceName,
            ServiceId = serviceName
        };
        configure?.Invoke(options);

        builder.UseOrleansSilo(config =>
        {
            config.ServiceId = options.ServiceId ?? serviceName;
            config.ClusterId = options.ClusterId;
            config.SiloPort = options.PrimaryPort;
            config.GatewayPort = options.ClientPort;
            if (!string.IsNullOrWhiteSpace(options.AdvertisedIPAddress))
            {
                config.AdvertisedIPAddress = options.AdvertisedIPAddress;
            }
            config.ClusterProvider = options.ClusterProvider switch
            {
                StatefulClusterProvider.Local => OrleansClusterProvider.Localhost,
                StatefulClusterProvider.Consul => OrleansClusterProvider.Consul,
                StatefulClusterProvider.AzureTable => OrleansClusterProvider.AzureTable,
                StatefulClusterProvider.AdoNet => OrleansClusterProvider.AdoNet,
                _ => OrleansClusterProvider.Localhost
            };
            config.ConsulAddress = options.ConsulAddress;
            config.ConsulToken = options.ConsulToken;
            config.DatabaseConnectionString = options.DatabaseConnectionString;
            config.DatabaseInvariant = options.DatabaseInvariant;
            config.AzureStorageConnectionString = options.AzureStorageConnectionString;
            config.EnableDashboard = options.EnableDashboard;
            config.DashboardPort = options.DashboardPort;
            config.RedisStorageConnectionString = options.RedisStorageConnectionString;
            config.RedisStorageDatabase = options.RedisStorageDatabase;
            config.RedisStorageKeyPrefix = options.RedisStorageKeyPrefix;
        });

        builder.ConfigureServices(services =>
        {
            services.AddMeshTelemetry(serviceName);
            services.AddMeshAuthorizationPolicies();
            services.AddSingleton(options);
        });

        return builder;
    }
}

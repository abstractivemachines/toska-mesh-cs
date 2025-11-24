using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Core.Configuration;
using ToskaMesh.Security;
using ToskaMesh.Telemetry;

namespace ToskaMesh.Runtime.Orleans;

/// <summary>
/// Convenience methods for wiring an Orleans silo with Toska Mesh defaults.
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
            config.SiloPort = options.SiloPort;
            config.GatewayPort = options.GatewayPort;
            config.ClusteringMode = options.ClusteringMode;
            config.ConsulAddress = options.ConsulAddress;
            config.ConsulToken = options.ConsulToken;
            config.DatabaseConnectionString = options.DatabaseConnectionString;
            config.DatabaseInvariant = options.DatabaseInvariant;
            config.AzureStorageConnectionString = options.AzureStorageConnectionString;
            config.EnableDashboard = options.EnableDashboard;
            config.DashboardPort = options.DashboardPort;
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

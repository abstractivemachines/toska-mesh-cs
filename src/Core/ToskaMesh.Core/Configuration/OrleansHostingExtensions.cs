using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansDashboard;

namespace ToskaMesh.Core.Configuration;

/// <summary>
/// Extension methods for configuring Orleans silo hosting.
/// </summary>
public static class OrleansHostingExtensions
{
    /// <summary>
    /// Adds Orleans silo to the host builder with standard Toska Mesh configuration.
    /// </summary>
    public static IHostBuilder UseOrleansSilo(
        this IHostBuilder builder,
        Action<OrleansClusterConfig>? configureOptions = null)
    {
        builder.UseOrleans((context, siloBuilder) =>
        {
            var config = new OrleansClusterConfig();
            context.Configuration.GetSection("Orleans").Bind(config);
            configureOptions?.Invoke(config);

            // Configure cluster
            siloBuilder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = config.ClusterId;
                options.ServiceId = config.ServiceId;
            });

            // Configure endpoints
            siloBuilder.ConfigureEndpoints(
                siloPort: config.SiloPort,
                gatewayPort: config.GatewayPort,
                advertisedIP: null,
                listenOnAnyHostAddress: true);

            // Configure clustering
            switch (config.ClusteringMode.ToLowerInvariant())
            {
                case "localhost":
                    siloBuilder.UseLocalhostClustering();
                    break;
                default:
                    throw new NotSupportedException($"Clustering mode '{config.ClusteringMode}' is not implemented yet.");
            }

            // Configure grain storage
            if (!string.IsNullOrEmpty(config.DatabaseConnectionString))
            {
                siloBuilder.AddAdoNetGrainStorage("Default", options =>
                {
                    options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                    options.ConnectionString = config.DatabaseConnectionString;
                });
            }

            // TODO: configure reminders + application parts when non-localhost clustering is implemented.

            // Configure dashboard (optional, for development)
            if (config.EnableDashboard)
            {
                siloBuilder.UseDashboard(options =>
                {
                    options.Port = config.DashboardPort;
                });
            }
        });

        return builder;
    }

    /// <summary>
    /// Adds Orleans client to the service collection with standard Toska Mesh configuration.
    /// </summary>
    public static IServiceCollection AddOrleansClient(
        this IServiceCollection services,
        Action<OrleansClusterConfig>? configureOptions = null)
    {
        services.AddOrleansClient(clientBuilder =>
        {
            var config = new OrleansClusterConfig();
            configureOptions?.Invoke(config);

            clientBuilder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = config.ClusterId;
                options.ServiceId = config.ServiceId;
            });

            // Configure clustering
            switch (config.ClusteringMode.ToLowerInvariant())
            {
                case "localhost":
                    clientBuilder.UseLocalhostClustering();
                    break;
                default:
                    throw new NotSupportedException($"Clustering mode '{config.ClusteringMode}' is not implemented yet.");
            }
        });

        return services;
    }
}

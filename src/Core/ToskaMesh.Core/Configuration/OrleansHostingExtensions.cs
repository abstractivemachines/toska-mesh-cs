using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansDashboard;
using System.Net;

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
                advertisedIP: IPAddress.Loopback,
                listenOnAnyHostAddress: true);

            // Configure clustering
            switch (config.ClusteringMode.ToLowerInvariant())
            {
                case "localhost":
                    siloBuilder.UseLocalhostClustering();
                    break;

                case "consul":
                    siloBuilder.UseConsulSiloClustering(options =>
                    {
                        options.KvRootFolder = $"orleans/{config.ClusterId}";
                        options.ConfigureConsulClient(new Uri(config.ConsulAddress ?? "http://localhost:8500"), config.ConsulToken);
                    });
                    break;

                case "azuretable":
#pragma warning disable CS0618
                    siloBuilder.UseAzureStorageClustering(options =>
                    {
                        options.ConfigureTableServiceClient(config.AzureStorageConnectionString);
                    });
#pragma warning restore CS0618
                    break;

                case "adonet":
                    siloBuilder.UseAdoNetClustering(options =>
                    {
                        options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                        options.ConnectionString = config.DatabaseConnectionString
                            ?? throw new InvalidOperationException("Database connection string is required for ADO.NET clustering.");
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unknown clustering mode: {config.ClusteringMode}");
            }

            // Configure grain storage
            if (!string.IsNullOrEmpty(config.DatabaseConnectionString))
            {
                siloBuilder.AddAdoNetGrainStorage("Default", options =>
                {
                    options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                    options.ConnectionString = config.DatabaseConnectionString;
                });

                siloBuilder.AddAdoNetGrainStorage("clusterStore", options =>
                {
                    options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                    options.ConnectionString = config.DatabaseConnectionString;
                });

                siloBuilder.UseAdoNetReminderService(options =>
                {
                    options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                    options.ConnectionString = config.DatabaseConnectionString;
                });
            }
            else
            {
                siloBuilder.AddMemoryGrainStorageAsDefault();
                siloBuilder.AddMemoryGrainStorage("clusterStore");
                siloBuilder.UseInMemoryReminderService();
            }

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

                case "consul":
                    clientBuilder.UseConsulClientClustering(options =>
                    {
                        options.KvRootFolder = $"orleans/{config.ClusterId}";
                        options.ConfigureConsulClient(new Uri(config.ConsulAddress ?? "http://localhost:8500"), config.ConsulToken);
                    });
                    break;

                case "azuretable":
#pragma warning disable CS0618
                    clientBuilder.UseAzureStorageClustering(options =>
                    {
                        options.ConfigureTableServiceClient(config.AzureStorageConnectionString);
                    });
#pragma warning restore CS0618
                    break;

                case "adonet":
                    clientBuilder.UseAdoNetClustering(options =>
                    {
                        options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                        options.ConnectionString = config.DatabaseConnectionString
                            ?? throw new InvalidOperationException("Database connection string is required for ADO.NET clustering.");
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unknown clustering mode: {config.ClusteringMode}");
            }
        });

        return services;
    }
}

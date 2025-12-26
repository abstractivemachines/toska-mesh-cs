using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansDashboard;
using System.Net;
using ToskaMesh.Core.Storage;

namespace ToskaMesh.Core.Configuration;

/// <summary>
/// Extension methods for configuring Orleans silo hosting.
/// </summary>
public static class OrleansHostingExtensions
{
    /// <summary>
    /// Adds Orleans silo to the host builder with standard ToskaMesh configuration.
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
            var advertisedIp = IPAddress.Loopback;
            if (!string.IsNullOrWhiteSpace(config.AdvertisedIPAddress) &&
                IPAddress.TryParse(config.AdvertisedIPAddress, out var parsedIp))
            {
                advertisedIp = parsedIp;
            }

            siloBuilder.ConfigureEndpoints(
                siloPort: config.SiloPort,
                gatewayPort: config.GatewayPort,
                advertisedIP: advertisedIp,
                listenOnAnyHostAddress: true);

            // Configure clustering
            switch (config.ClusterProvider)
            {
                case OrleansClusterProvider.Localhost:
                    siloBuilder.UseLocalhostClustering();
                    break;

                case OrleansClusterProvider.Consul:
                    siloBuilder.UseConsulSiloClustering(options =>
                    {
                        options.KvRootFolder = $"orleans/{config.ClusterId}";
                        options.ConfigureConsulClient(new Uri(config.ConsulAddress ?? "http://localhost:8500"), config.ConsulToken);
                    });
                    break;

                case OrleansClusterProvider.AzureTable:
#pragma warning disable CS0618
                    siloBuilder.UseAzureStorageClustering(options =>
                    {
                        options.ConfigureTableServiceClient(config.AzureStorageConnectionString);
                    });
#pragma warning restore CS0618
                    break;

                case OrleansClusterProvider.AdoNet:
                    siloBuilder.UseAdoNetClustering(options =>
                    {
                        options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                        options.ConnectionString = config.DatabaseConnectionString
                            ?? throw new InvalidOperationException("Database connection string is required for ADO.NET clustering.");
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unknown clustering mode: {config.ClusterProvider}");
            }

            // Configure grain storage
            if (!string.IsNullOrEmpty(config.RedisStorageConnectionString))
            {
                var basePrefix = string.IsNullOrWhiteSpace(config.RedisStorageKeyPrefix)
                    ? $"{config.ServiceId}:grain:"
                    : config.RedisStorageKeyPrefix!;
                var clusterPrefix = basePrefix.EndsWith(":", StringComparison.Ordinal)
                    ? $"{basePrefix}cluster:"
                    : $"{basePrefix}:cluster:";

                siloBuilder.AddRedisGrainStorageAsDefault(options =>
                {
                    options.ConnectionString = config.RedisStorageConnectionString!;
                    options.Database = config.RedisStorageDatabase;
                    options.KeyPrefix = basePrefix;
                });

                siloBuilder.AddRedisGrainStorage("clusterStore", options =>
                {
                    options.ConnectionString = config.RedisStorageConnectionString!;
                    options.Database = config.RedisStorageDatabase;
                    options.KeyPrefix = clusterPrefix;
                });

                siloBuilder.UseInMemoryReminderService();
            }
            else if (!string.IsNullOrEmpty(config.DatabaseConnectionString))
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
    /// Adds Orleans client to the service collection with standard ToskaMesh configuration.
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
            switch (config.ClusterProvider)
            {
                case OrleansClusterProvider.Localhost:
                    clientBuilder.UseLocalhostClustering();
                    break;

                case OrleansClusterProvider.Consul:
                    clientBuilder.UseConsulClientClustering(options =>
                    {
                        options.KvRootFolder = $"orleans/{config.ClusterId}";
                        options.ConfigureConsulClient(new Uri(config.ConsulAddress ?? "http://localhost:8500"), config.ConsulToken);
                    });
                    break;

                case OrleansClusterProvider.AzureTable:
#pragma warning disable CS0618
                    clientBuilder.UseAzureStorageClustering(options =>
                    {
                        options.ConfigureTableServiceClient(config.AzureStorageConnectionString);
                    });
#pragma warning restore CS0618
                    break;

                case OrleansClusterProvider.AdoNet:
                    clientBuilder.UseAdoNetClustering(options =>
                    {
                        options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                        options.ConnectionString = config.DatabaseConnectionString
                            ?? throw new InvalidOperationException("Database connection string is required for ADO.NET clustering.");
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unknown clustering mode: {config.ClusterProvider}");
            }
        });

        return services;
    }
}

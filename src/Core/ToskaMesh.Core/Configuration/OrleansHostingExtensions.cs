using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;

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

                case "consul":
                    siloBuilder.UseConsulClustering(options =>
                    {
                        options.Address = new Uri(config.ConsulAddress!);
                    });
                    break;

                case "azuretable":
                    siloBuilder.UseAzureStorageClustering(options =>
                    {
                        options.ConfigureTableServiceClient(config.AzureStorageConnectionString);
                    });
                    break;

                case "adonet":
                    siloBuilder.UseAdoNetClustering(options =>
                    {
                        options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                        options.ConnectionString = config.DatabaseConnectionString!;
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
            }

            // Configure reminders
            if (!string.IsNullOrEmpty(config.DatabaseConnectionString))
            {
                siloBuilder.UseAdoNetReminderService(options =>
                {
                    options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                    options.ConnectionString = config.DatabaseConnectionString;
                });
            }

            // Configure application parts (scan for grains)
            siloBuilder.ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(OrleansHostingExtensions).Assembly)
                    .WithReferences();
            });

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
                    clientBuilder.UseConsulClustering(options =>
                    {
                        options.Address = new Uri(config.ConsulAddress!);
                    });
                    break;

                case "azuretable":
                    clientBuilder.UseAzureStorageClustering(options =>
                    {
                        options.ConfigureTableServiceClient(config.AzureStorageConnectionString);
                    });
                    break;

                case "adonet":
                    clientBuilder.UseAdoNetClustering(options =>
                    {
                        options.Invariant = config.DatabaseInvariant ?? "Npgsql";
                        options.ConnectionString = config.DatabaseConnectionString!;
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unknown clustering mode: {config.ClusteringMode}");
            }

            // Configure application parts
            clientBuilder.ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(OrleansHostingExtensions).Assembly)
                    .WithReferences();
            });
        });

        return services;
    }
}

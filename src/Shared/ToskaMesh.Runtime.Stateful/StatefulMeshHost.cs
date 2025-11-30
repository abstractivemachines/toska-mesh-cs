using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Orleans;

namespace ToskaMesh.Runtime.Stateful;

/// <summary>
/// Provider-agnostic entry points for hosting stateful Toska Mesh services.
/// </summary>
public static class StatefulMeshHost
{
    public static async Task RunAsync(
        Action<StatefulHostOptions>? configureStateful = null,
        Action<MeshServiceOptions>? configureService = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        using var host = BuildHost(configureStateful, configureService, configureServices);
        await host.StartAsync(cancellationToken);
        await host.WaitForShutdownAsync(cancellationToken);
    }

    public static IHost Start(
        Action<StatefulHostOptions>? configureStateful = null,
        Action<MeshServiceOptions>? configureService = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = BuildHost(configureStateful, configureService, configureServices);
        host.Start();
        return host;
    }

    private static IHost BuildHost(
        Action<StatefulHostOptions>? configureStateful,
        Action<MeshServiceOptions>? configureService,
        Action<IServiceCollection>? configureServices)
    {
        var statefulOptions = new StatefulHostOptions();
        configureStateful?.Invoke(statefulOptions);
        statefulOptions.EnsureDefaults();

        var builder = Host.CreateDefaultBuilder();

        ConfigureProvider(builder, statefulOptions);

        builder.ConfigureServices((context, services) =>
        {
            services.AddSingleton(statefulOptions);
            services.AddSingleton(statefulOptions.Orleans);

            services.AddMeshService(context.Configuration, opts =>
            {
                opts.ServiceName = statefulOptions.ServiceName;
                opts.ServiceId = statefulOptions.ServiceId;
                configureService?.Invoke(opts);
            });

            if (statefulOptions.KeyValue.Enabled)
            {
                services.AddMeshKeyValueStore(context.Configuration, redis =>
                {
                    if (!string.IsNullOrWhiteSpace(statefulOptions.KeyValue.ConnectionString))
                    {
                        redis.ConnectionString = statefulOptions.KeyValue.ConnectionString!;
                    }

                    if (statefulOptions.KeyValue.Database.HasValue)
                    {
                        redis.Database = statefulOptions.KeyValue.Database;
                    }

                    redis.KeyPrefix ??= statefulOptions.KeyValue.KeyPrefix;
                });
            }

            configureServices?.Invoke(services);
        });

        return builder.Build();
    }

    private static void ConfigureProvider(IHostBuilder builder, StatefulHostOptions options)
    {
        switch (options.Provider)
        {
            case StatefulRuntimeProvider.Orleans:
                builder.UseMeshSilo(options.ServiceName, silo => options.Orleans.ApplyTo(silo, options.ServiceName, options.ServiceId));
                break;
            default:
                throw new NotSupportedException($"Stateful provider '{options.Provider}' is not supported.");
        }
    }
}

/// <summary>
/// High-level options for stateful hosting.
/// </summary>
public class StatefulHostOptions
{
    public string ServiceName { get; set; } = "mesh-stateful-service";
    public string? ServiceId { get; set; }
    public StatefulRuntimeProvider Provider { get; set; } = StatefulRuntimeProvider.Orleans;
    public OrleansProviderOptions Orleans { get; } = new();
    public StatefulKeyValueOptions KeyValue { get; } = new();

    internal void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("Stateful services require a ServiceName.");
        }

        ServiceId ??= ServiceName;
        Orleans.EnsureDefaults();
        KeyValue.EnsureDefaults(ServiceName);
    }
}

public enum StatefulRuntimeProvider
{
    Orleans
}

/// <summary>
/// Provider-specific options for the Orleans-backed stateful runtime.
/// </summary>
public class OrleansProviderOptions
{
    public string ClusterId { get; set; } = "toska-mesh";
    public int PrimaryPort { get; set; } = 11111;
    public int ClientPort { get; set; } = 30000;
    public string? AdvertisedIPAddress { get; set; }
    public StatefulClusterProvider ClusterProvider { get; set; } = StatefulClusterProvider.Local;
    public string? ConsulAddress { get; set; } = "http://localhost:8500";
    public string? ConsulToken { get; set; }
    public string? DatabaseConnectionString { get; set; }
    public string? DatabaseInvariant { get; set; } = "Npgsql";
    public string? AzureStorageConnectionString { get; set; }
    public bool EnableDashboard { get; set; }
    public int DashboardPort { get; set; } = 8080;

    internal void EnsureDefaults()
    {
        if (PrimaryPort <= 0)
        {
            PrimaryPort = 11111;
        }

        if (ClientPort <= 0)
        {
            ClientPort = 30000;
        }

        if (string.IsNullOrWhiteSpace(ClusterId))
        {
            ClusterId = "toska-mesh";
        }

        if (DashboardPort <= 0)
        {
            DashboardPort = 8080;
        }
    }

    internal void ApplyTo(MeshStatefulOptions target, string serviceName, string? serviceId)
    {
        target.ServiceId = serviceId ?? serviceName;
        target.ServiceName = serviceName;
        target.ClusterId = ClusterId;
        target.PrimaryPort = PrimaryPort;
        target.ClientPort = ClientPort;
        target.AdvertisedIPAddress = AdvertisedIPAddress;
        target.ClusterProvider = ClusterProvider.ToOrleansProvider();
        target.ConsulAddress = ConsulAddress;
        target.ConsulToken = ConsulToken;
        target.DatabaseConnectionString = DatabaseConnectionString;
        target.DatabaseInvariant = DatabaseInvariant;
        target.AzureStorageConnectionString = AzureStorageConnectionString;
        target.EnableDashboard = EnableDashboard;
        target.DashboardPort = DashboardPort;
    }
}

/// <summary>
/// Cluster provider options for the stateful host (provider-neutral).
/// </summary>
public enum StatefulClusterProvider
{
    Local,
    Consul,
    AzureTable,
    AdoNet
}

internal static class StatefulClusterProviderExtensions
{
    public static ToskaMesh.Runtime.Orleans.StatefulClusterProvider ToOrleansProvider(this StatefulClusterProvider provider) =>
        provider switch
        {
            StatefulClusterProvider.Local => ToskaMesh.Runtime.Orleans.StatefulClusterProvider.Local,
            StatefulClusterProvider.Consul => ToskaMesh.Runtime.Orleans.StatefulClusterProvider.Consul,
            StatefulClusterProvider.AzureTable => ToskaMesh.Runtime.Orleans.StatefulClusterProvider.AzureTable,
            StatefulClusterProvider.AdoNet => ToskaMesh.Runtime.Orleans.StatefulClusterProvider.AdoNet,
            _ => ToskaMesh.Runtime.Orleans.StatefulClusterProvider.Local
        };
}

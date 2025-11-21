using Microsoft.Extensions.Primitives;
using ToskaMesh.Protocols;
using Yarp.ReverseProxy.Configuration;

namespace ToskaMesh.Gateway.Services;

/// <summary>
/// Custom YARP configuration provider that reads service instances from Consul.
/// </summary>
public class ConsulProxyConfigProvider : IProxyConfigProvider
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly ILogger<ConsulProxyConfigProvider> _logger;
    private volatile ConsulProxyConfig _config;
    private CancellationTokenSource _changeToken;
    private bool _disposed;

    public ConsulProxyConfigProvider(
        IServiceRegistry serviceRegistry,
        ILogger<ConsulProxyConfigProvider> logger)
    {
        _serviceRegistry = serviceRegistry;
        _logger = logger;
        _config = new ConsulProxyConfig(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());
        _changeToken = new CancellationTokenSource();

        // Start background refresh
        _ = RefreshConfigAsync();
    }

    public IProxyConfig GetConfig() => _config;

    private async Task RefreshConfigAsync()
    {
        while (!_disposed)
        {
            try
            {
                await UpdateConfigAsync();
                await Task.Delay(TimeSpan.FromSeconds(30)); // Refresh every 30 seconds
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing proxy configuration from Consul");
                await Task.Delay(TimeSpan.FromSeconds(10)); // Shorter retry interval
            }
        }
    }

    private async Task UpdateConfigAsync()
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        // Get all service names from Consul
        var serviceNames = await _serviceRegistry.GetAllServicesAsync();

        foreach (var serviceName in serviceNames)
        {
            // Skip internal Consul services
            if (serviceName.StartsWith("consul", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Get healthy instances for this service
            var instances = await _serviceRegistry.GetServiceInstancesAsync(serviceName);
            var healthyInstances = instances.Where(i => i.Status == HealthStatus.Healthy).ToList();

            if (!healthyInstances.Any())
            {
                _logger.LogWarning("No healthy instances found for service {ServiceName}", serviceName);
                continue;
            }

            // Create route for this service
            var routeId = $"{serviceName}-route";
            var clusterId = $"{serviceName}-cluster";

            routes.Add(new RouteConfig
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    Path = $"/api/{serviceName}/{{**catch-all}}"
                },
                Transforms = new[]
                {
                    new Dictionary<string, string>
                    {
                        { "PathPattern", "/{**catch-all}" }
                    }
                }
            });

            // Create cluster with all healthy destinations
            var destinations = new Dictionary<string, DestinationConfig>();
            foreach (var instance in healthyInstances)
            {
                var scheme = instance.Metadata.GetValueOrDefault("scheme", "http");
                destinations[instance.ServiceId] = new DestinationConfig
                {
                    Address = $"{scheme}://{instance.Address}:{instance.Port}"
                };
            }

            clusters.Add(new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = destinations,
                LoadBalancingPolicy = "RoundRobin",
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Interval = TimeSpan.FromSeconds(10),
                        Timeout = TimeSpan.FromSeconds(5),
                        Policy = "ConsecutiveFailures",
                        Path = "/health"
                    }
                }
            });
        }

        var oldConfig = _config;
        _config = new ConsulProxyConfig(routes, clusters);

        // Signal configuration change
        var oldChangeToken = _changeToken;
        _changeToken = new CancellationTokenSource();
        oldChangeToken.Cancel();

        _logger.LogInformation("Updated proxy configuration: {RouteCount} routes, {ClusterCount} clusters",
            routes.Count, clusters.Count);
    }

    public void Dispose()
    {
        _disposed = true;
        _changeToken?.Dispose();
    }

    private class ConsulProxyConfig : IProxyConfig
    {
        private readonly CancellationChangeToken _changeToken;

        public ConsulProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            _changeToken = new CancellationChangeToken(new CancellationToken());
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken => _changeToken;
    }
}

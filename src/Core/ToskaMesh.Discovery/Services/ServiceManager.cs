using System.Collections.Concurrent;
using System.Linq;
using MassTransit;
using ToskaMesh.Common.Messaging;
using ToskaMesh.Discovery.Models;
using ToskaMesh.Protocols;

namespace ToskaMesh.Discovery.Services;

/// <summary>
/// Implementation of service manager using Consul service registry.
/// </summary>
public class ServiceManager : IServiceManager
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ServiceManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ServiceInstanceTrackingInfo> _tracking;

    public ServiceManager(
        IServiceRegistry serviceRegistry,
        IPublishEndpoint publishEndpoint,
        ILogger<ServiceManager> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _serviceRegistry = serviceRegistry;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
        _tracking = new ConcurrentDictionary<string, ServiceInstanceTrackingInfo>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering service: {ServiceName} ({ServiceId})",
            registration.ServiceName, registration.ServiceId);

        var result = await _serviceRegistry.RegisterAsync(registration, cancellationToken);

        if (result.Success)
        {
            TrackRegistration(registration);

            // Publish service registered event
            await _publishEndpoint.Publish(new ServiceRegisteredEvent
            {
                ServiceId = registration.ServiceId,
                ServiceName = registration.ServiceName,
                Address = registration.Address,
                Port = registration.Port,
                Metadata = registration.Metadata
            }, cancellationToken);

            _logger.LogInformation("Service registered successfully: {ServiceId}", registration.ServiceId);
        }
        else
        {
            _logger.LogWarning("Failed to register service: {ServiceId} - {Error}", registration.ServiceId, result.ErrorMessage);
        }

        return result;
    }

    public async Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deregistering service: {ServiceId}", serviceId);

        // Get service info before deregistering
        var instance = await _serviceRegistry.GetServiceInstanceAsync(serviceId, cancellationToken);

        var result = await _serviceRegistry.DeregisterAsync(serviceId, cancellationToken);

        if (result && instance != null)
        {
            UpdateTracking(serviceId, tracking =>
            {
                tracking.DeregisteredAt = DateTime.UtcNow;
            });

            // Publish service deregistered event
            await _publishEndpoint.Publish(new ServiceDeregisteredEvent
            {
                ServiceId = serviceId,
                ServiceName = instance.ServiceName,
                Reason = "Manual deregistration"
            }, cancellationToken);

            _logger.LogInformation("Service deregistered successfully: {ServiceId}", serviceId);
        }
        else
        {
            _logger.LogWarning("Failed to deregister service: {ServiceId}", serviceId);
        }

        return result;
    }

    public async Task<IEnumerable<ServiceInstance>> GetInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var instances = await _serviceRegistry.GetServiceInstancesAsync(serviceName, cancellationToken);
        return instances.Select(instance => ApplyTrackingMetadata(instance)).ToList();
    }

    public async Task<ServiceInstance?> GetInstanceAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var instance = await _serviceRegistry.GetServiceInstanceAsync(serviceId, cancellationToken);
        return instance == null ? null : ApplyTrackingMetadata(instance);
    }

    public Task<ServiceInstanceTrackingSnapshot?> GetTrackingAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        if (_tracking.TryGetValue(serviceId, out var info))
        {
            return Task.FromResult<ServiceInstanceTrackingSnapshot?>(info.ToSnapshot());
        }

        return Task.FromResult<ServiceInstanceTrackingSnapshot?>(null);
    }

    public Task<IEnumerable<ServiceInstanceTrackingSnapshot>> GetTrackingForServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var snapshots = _tracking.Values
            .Where(t => string.Equals(t.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.ToSnapshot())
            .ToList();

        return Task.FromResult<IEnumerable<ServiceInstanceTrackingSnapshot>>(snapshots);
    }

    public Task<ServiceMetadataSummary> GetMetadataSummaryAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var snapshots = _tracking.Values
            .Where(t => string.Equals(t.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.ToSnapshot())
            .ToList();

        var keySummaries = snapshots
            .SelectMany(snapshot => snapshot.Metadata)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new MetadataKeySummary(
                group.Key,
                group.Count(),
                group.Select(v => v.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();

        var summary = new ServiceMetadataSummary(
            serviceName,
            snapshots.Count,
            DateTime.UtcNow,
            keySummaries);

        return Task.FromResult(summary);
    }

    public async Task<IEnumerable<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
    {
        return await _serviceRegistry.GetAllServicesAsync(cancellationToken);
    }

    public async Task<bool> UpdateHealthAsync(string serviceId, HealthStatus status, string? output = null, CancellationToken cancellationToken = default)
    {
        var instance = await _serviceRegistry.GetServiceInstanceAsync(serviceId, cancellationToken);
        if (instance == null)
        {
            return false;
        }

        var previousStatus = instance.Status;
        var result = await _serviceRegistry.UpdateHealthStatusAsync(serviceId, status, cancellationToken);

        if (result)
        {
            UpdateTracking(serviceId, info =>
            {
                info.Status = status;
                info.LastHealthCheck = DateTime.UtcNow;
            });

            if (previousStatus != status)
            {
                // Publish health changed event
                await _publishEndpoint.Publish(new ServiceHealthChangedEvent
                {
                    ServiceId = serviceId,
                    ServiceName = instance.ServiceName,
                    PreviousStatus = previousStatus.ToString(),
                    CurrentStatus = status.ToString(),
                    HealthCheckOutput = output
                }, cancellationToken);
            }
        }

        return result;
    }

    public async Task PerformHealthChecksAsync(CancellationToken cancellationToken = default)
    {
        var serviceNames = await _serviceRegistry.GetAllServicesAsync(cancellationToken);

        foreach (var serviceName in serviceNames)
        {
            var instances = await _serviceRegistry.GetServiceInstancesAsync(serviceName, cancellationToken);

            foreach (var instance in instances)
            {
                if (string.IsNullOrEmpty(instance.Metadata.GetValueOrDefault("health_check_endpoint")))
                {
                    continue;
                }

                TrackFromInstance(instance);
                await CheckServiceHealthAsync(instance, cancellationToken);
            }
        }
    }

    public async Task<bool> TriggerHealthCheckAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var instance = await _serviceRegistry.GetServiceInstanceAsync(serviceId, cancellationToken);
        if (instance == null)
        {
            return false;
        }

        TrackFromInstance(instance);
        await CheckServiceHealthAsync(instance, cancellationToken);
        return true;
    }

    public async Task<bool> UpdateMetadataAsync(string serviceId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        if (!_tracking.ContainsKey(serviceId))
        {
            var instance = await _serviceRegistry.GetServiceInstanceAsync(serviceId, cancellationToken);
            if (instance == null)
            {
                return false;
            }

            TrackFromInstance(instance);
        }

        UpdateTracking(serviceId, info =>
        {
            info.Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        });

        return true;
    }

    private async Task CheckServiceHealthAsync(ServiceInstance instance, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = _tracking.TryGetValue(instance.ServiceId, out var tracking)
                ? tracking.ToSnapshot().Metadata
                : instance.Metadata;

            var healthEndpoint = metadata.GetValueOrDefault("health_check_endpoint", "/health");
            var scheme = metadata.GetValueOrDefault("scheme", "http");
            var url = $"{scheme}://{instance.Address}:{instance.Port}{healthEndpoint}";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            var newStatus = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            var output = $"HTTP {(int)response.StatusCode}";

            await UpdateHealthAsync(instance.ServiceId, newStatus, output, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for service {ServiceId}", instance.ServiceId);
            await UpdateHealthAsync(instance.ServiceId, HealthStatus.Unhealthy, ex.Message, cancellationToken);
        }
    }

    private void TrackRegistration(ServiceRegistration registration)
    {
        var info = _tracking.GetOrAdd(registration.ServiceId, _ => new ServiceInstanceTrackingInfo(registration.ServiceId, registration.ServiceName));
        info.Update(tracking =>
        {
            tracking.ServiceName = registration.ServiceName;
            tracking.Metadata = new Dictionary<string, string>(registration.Metadata, StringComparer.OrdinalIgnoreCase);
            tracking.Status = HealthStatus.Unknown;
            tracking.DeregisteredAt = null;
        });
    }

    private void TrackFromInstance(ServiceInstance instance)
    {
        var info = _tracking.GetOrAdd(instance.ServiceId, _ => ServiceInstanceTrackingInfo.FromInstance(instance));
        info.Update(tracking =>
        {
            tracking.ServiceName = instance.ServiceName;
            tracking.Metadata = new Dictionary<string, string>(instance.Metadata, StringComparer.OrdinalIgnoreCase);
            tracking.Status = instance.Status;
        });
    }

    private void UpdateTracking(string serviceId, Action<ServiceInstanceTrackingInfo> update)
    {
        if (_tracking.TryGetValue(serviceId, out var info))
        {
            info.Update(update);
        }
    }

    private ServiceInstance ApplyTrackingMetadata(ServiceInstance instance)
    {
        if (_tracking.TryGetValue(instance.ServiceId, out var info))
        {
            var snapshot = info.ToSnapshot();
            if (snapshot.Metadata.Count > 0)
            {
                var merged = new Dictionary<string, string>(instance.Metadata, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in snapshot.Metadata)
                {
                    merged[kv.Key] = kv.Value;
                }

                instance = instance with { Metadata = merged };
            }
        }

        return instance;
    }

    private sealed class ServiceInstanceTrackingInfo
    {
        private readonly object _lock = new();

        public ServiceInstanceTrackingInfo(string serviceId, string serviceName)
        {
            ServiceId = serviceId;
            ServiceName = serviceName;
            RegisteredAt = DateTime.UtcNow;
            LastUpdated = DateTime.UtcNow;
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string ServiceId { get; }
        public string ServiceName { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? DeregisteredAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public HealthStatus Status { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public void Update(Action<ServiceInstanceTrackingInfo> updater)
        {
            lock (_lock)
            {
                updater(this);
                LastUpdated = DateTime.UtcNow;
            }
        }

        public ServiceInstanceTrackingSnapshot ToSnapshot()
        {
            lock (_lock)
            {
                return new ServiceInstanceTrackingSnapshot(
                    ServiceId,
                    ServiceName,
                    RegisteredAt,
                    DeregisteredAt,
                    LastUpdated,
                    Status,
                    LastHealthCheck,
                    new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase));
            }
        }

        public static ServiceInstanceTrackingInfo FromInstance(ServiceInstance instance)
        {
            var info = new ServiceInstanceTrackingInfo(instance.ServiceId, instance.ServiceName)
            {
                Status = instance.Status,
                Metadata = new Dictionary<string, string>(instance.Metadata, StringComparer.OrdinalIgnoreCase),
                RegisteredAt = instance.RegisteredAt,
                LastHealthCheck = instance.LastHealthCheck
            };

            return info;
        }
    }
}

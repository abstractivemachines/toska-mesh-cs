using Consul;
using Microsoft.Extensions.Logging;
using ToskaMesh.Protocols;
using ConsulHealthStatus = Consul.HealthStatus;
using MeshHealthStatus = ToskaMesh.Protocols.HealthStatus;

namespace ToskaMesh.Common.ServiceDiscovery;

/// <summary>
/// Implementation of IServiceRegistry using Consul.
/// </summary>
public class ConsulServiceRegistry : IServiceRegistry
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulServiceRegistry> _logger;

    public ConsulServiceRegistry(
        IConsulClient consulClient,
        ILogger<ConsulServiceRegistry> logger)
    {
        _consulClient = consulClient;
        _logger = logger;
    }

    public async Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
    {
        try
        {
            var ttlInterval = registration.HealthCheck?.Interval;
            if (ttlInterval == null || ttlInterval.Value <= TimeSpan.Zero)
            {
                ttlInterval = TimeSpan.FromSeconds(30);
            }

            // Provide a small buffer to avoid accidental expirations between updates.
            var ttlWithBuffer = ttlInterval.Value + TimeSpan.FromSeconds(5);
            if (ttlWithBuffer < TimeSpan.FromSeconds(10))
            {
                ttlWithBuffer = TimeSpan.FromSeconds(10);
            }
            var ttlCheckId = $"service:{registration.ServiceId}";

            var consulRegistration = new AgentServiceRegistration
            {
                ID = registration.ServiceId,
                Name = registration.ServiceName,
                Address = registration.Address,
                Port = registration.Port,
                Meta = registration.Metadata,
                Check = new AgentServiceCheck
                {
                    CheckID = ttlCheckId,
                    Name = $"{registration.ServiceName} TTL Health",
                    TTL = ttlWithBuffer,
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
                }
            };

            await _consulClient.Agent.ServiceRegister(consulRegistration, cancellationToken);

            // Mark the TTL check as passing so the service starts healthy until the first update.
            await _consulClient.Agent.PassTTL(ttlCheckId, "Service registered", cancellationToken);

            _logger.LogInformation("Service registered: {ServiceName} ({ServiceId}) at {Address}:{Port}",
                registration.ServiceName, registration.ServiceId, registration.Address, registration.Port);

            return new ServiceRegistrationResult(true, registration.ServiceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register service {ServiceId}", registration.ServiceId);
            return new ServiceRegistrationResult(false, registration.ServiceId, ex.Message);
        }
    }

    public async Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _consulClient.Agent.ServiceDeregister(serviceId, cancellationToken);
            _logger.LogInformation("Service deregistered: {ServiceId}", serviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deregister service {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var queryResult = await _consulClient.Health.Service(serviceName, null, false, cancellationToken);

            return queryResult.Response.Select(entry => new ServiceInstance(
                ServiceName: entry.Service.Service,
                ServiceId: entry.Service.ID,
                Address: entry.Service.Address,
                Port: entry.Service.Port,
                Status: MapHealthStatus(entry.Checks),
                Metadata: entry.Service.Meta?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>(),
                RegisteredAt: DateTime.UtcNow, // Consul doesn't track registration time, using current time
                LastHealthCheck: DateTime.UtcNow // Using current time as approximation
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service instances for {ServiceName}", serviceName);
            return Enumerable.Empty<ServiceInstance>();
        }
    }

    public async Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var services = await _consulClient.Agent.Services(cancellationToken);

            if (!services.Response.TryGetValue(serviceId, out var service))
            {
                return null;
            }

            var checks = await _consulClient.Health.Checks(service.Service, cancellationToken);

            return new ServiceInstance(
                ServiceName: service.Service,
                ServiceId: service.ID,
                Address: service.Address,
                Port: service.Port,
                Status: MapHealthStatus(checks.Response),
                Metadata: service.Meta?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>(),
                RegisteredAt: DateTime.UtcNow,
                LastHealthCheck: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service instance {ServiceId}", serviceId);
            return null;
        }
    }

    public async Task<bool> UpdateHealthStatusAsync(string serviceId, MeshHealthStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            var checkId = $"service:{serviceId}";

            switch (status)
            {
                case MeshHealthStatus.Healthy:
                    await _consulClient.Agent.PassTTL(checkId, "Service is healthy", cancellationToken);
                    break;
                case MeshHealthStatus.Unhealthy:
                    await _consulClient.Agent.FailTTL(checkId, "Service is unhealthy", cancellationToken);
                    break;
                case MeshHealthStatus.Degraded:
                    await _consulClient.Agent.WarnTTL(checkId, "Service is degraded", cancellationToken);
                    break;
            }

            _logger.LogInformation("Service health updated: {ServiceId} -> {Status}", serviceId, status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update service health for {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var services = await _consulClient.Catalog.Services(cancellationToken);
            return services.Response.Keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service names");
            return Enumerable.Empty<string>();
        }
    }

    private static MeshHealthStatus MapHealthStatus(HealthCheck[] checks)
    {
        if (checks == null || checks.Length == 0)
        {
            return MeshHealthStatus.Unknown;
        }

        if (checks.Any(c => c.Status == ConsulHealthStatus.Critical || c.Status == ConsulHealthStatus.Maintenance))
        {
            return MeshHealthStatus.Unhealthy;
        }

        if (checks.Any(c => c.Status == ConsulHealthStatus.Warning))
        {
            return MeshHealthStatus.Degraded;
        }

        return checks.All(c => c.Status == ConsulHealthStatus.Passing) ? MeshHealthStatus.Healthy : MeshHealthStatus.Unknown;
    }
}

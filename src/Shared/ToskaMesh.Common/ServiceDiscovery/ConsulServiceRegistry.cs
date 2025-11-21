using Consul;
using Microsoft.Extensions.Logging;
using ToskaMesh.Protocols;

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

    public async Task<bool> RegisterServiceAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
    {
        try
        {
            var consulRegistration = new AgentServiceRegistration
            {
                ID = registration.ServiceId,
                Name = registration.ServiceName,
                Address = registration.Address,
                Port = registration.Port,
                Tags = registration.Tags.ToArray(),
                Meta = registration.Metadata,
                Check = registration.HealthCheckEndpoint != null ? new AgentServiceCheck
                {
                    HTTP = $"{(registration.Metadata.ContainsKey("scheme") ? registration.Metadata["scheme"] : "http")}://{registration.Address}:{registration.Port}{registration.HealthCheckEndpoint}",
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5),
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
                } : null
            };

            await _consulClient.Agent.ServiceRegister(consulRegistration, cancellationToken);
            _logger.LogInformation("Service registered: {ServiceName} ({ServiceId}) at {Address}:{Port}",
                registration.ServiceName, registration.ServiceId, registration.Address, registration.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register service {ServiceId}", registration.ServiceId);
            return false;
        }
    }

    public async Task<bool> DeregisterServiceAsync(string serviceId, CancellationToken cancellationToken = default)
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
            var queryResult = await _consulClient.Health.Service(serviceName, null, true, cancellationToken);

            return queryResult.Response.Select(entry => new ServiceInstance
            {
                ServiceId = entry.Service.ID,
                ServiceName = entry.Service.Service,
                Address = entry.Service.Address,
                Port = entry.Service.Port,
                Tags = entry.Service.Tags?.ToList() ?? new List<string>(),
                Metadata = entry.Service.Meta ?? new Dictionary<string, string>(),
                HealthStatus = MapHealthStatus(entry.Checks)
            }).ToList();
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

            return new ServiceInstance
            {
                ServiceId = service.ID,
                ServiceName = service.Service,
                Address = service.Address,
                Port = service.Port,
                Tags = service.Tags?.ToList() ?? new List<string>(),
                Metadata = service.Meta ?? new Dictionary<string, string>(),
                HealthStatus = MapHealthStatus(checks.Response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service instance {ServiceId}", serviceId);
            return null;
        }
    }

    public async Task<bool> UpdateServiceHealthAsync(string serviceId, HealthStatus status, string? output = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var checkId = $"service:{serviceId}";

            switch (status)
            {
                case HealthStatus.Healthy:
                    await _consulClient.Agent.PassTTL(checkId, output ?? "Service is healthy", cancellationToken);
                    break;
                case HealthStatus.Unhealthy:
                    await _consulClient.Agent.FailTTL(checkId, output ?? "Service is unhealthy", cancellationToken);
                    break;
                case HealthStatus.Warning:
                    await _consulClient.Agent.WarnTTL(checkId, output ?? "Service has warnings", cancellationToken);
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

    public async Task<IEnumerable<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
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

    private static HealthStatus MapHealthStatus(HealthCheck[] checks)
    {
        if (checks == null || checks.Length == 0)
        {
            return HealthStatus.Unknown;
        }

        if (checks.Any(c => c.Status == HealthStatus.Critical || c.Status == HealthStatus.Maintenance))
        {
            return HealthStatus.Unhealthy;
        }

        if (checks.Any(c => c.Status == HealthStatus.Warning))
        {
            return HealthStatus.Warning;
        }

        return checks.All(c => c.Status == HealthStatus.Passing) ? HealthStatus.Healthy : HealthStatus.Unknown;
    }
}

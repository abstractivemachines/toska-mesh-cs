using ToskaMesh.Protocols;
using ToskaMesh.Common.Messaging;
using MassTransit;

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
    }

    public async Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering service: {ServiceName} ({ServiceId})",
            registration.ServiceName, registration.ServiceId);

        var result = await _serviceRegistry.RegisterAsync(registration, cancellationToken);

        if (result.Success)
        {
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
        return await _serviceRegistry.GetServiceInstancesAsync(serviceName, cancellationToken);
    }

    public async Task<ServiceInstance?> GetInstanceAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        return await _serviceRegistry.GetServiceInstanceAsync(serviceId, cancellationToken);
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

        if (result && previousStatus != status)
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

                await CheckServiceHealthAsync(instance, cancellationToken);
            }
        }
    }

    private async Task CheckServiceHealthAsync(ServiceInstance instance, CancellationToken cancellationToken)
    {
        try
        {
            var healthEndpoint = instance.Metadata.GetValueOrDefault("health_check_endpoint", "/health");
            var scheme = instance.Metadata.GetValueOrDefault("scheme", "http");
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
}

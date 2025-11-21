using ToskaMesh.Protocols;

namespace ToskaMesh.Discovery.Services;

/// <summary>
/// Service manager for handling service registration, discovery, and health monitoring.
/// </summary>
public interface IServiceManager
{
    /// <summary>
    /// Registers a new service instance.
    /// </summary>
    Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters a service instance.
    /// </summary>
    Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all instances of a service by name.
    /// </summary>
    Task<IEnumerable<ServiceInstance>> GetInstancesAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific service instance by ID.
    /// </summary>
    Task<ServiceInstance?> GetInstanceAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered service names.
    /// </summary>
    Task<IEnumerable<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the health status of a service.
    /// </summary>
    Task<bool> UpdateHealthAsync(string serviceId, HealthStatus status, string? output = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs health checks on all registered services.
    /// </summary>
    Task PerformHealthChecksAsync(CancellationToken cancellationToken = default);
}

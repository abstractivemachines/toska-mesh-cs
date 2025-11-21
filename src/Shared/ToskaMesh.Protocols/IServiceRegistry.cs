namespace ToskaMesh.Protocols;

/// <summary>
/// Service registry interface for service discovery
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// Register a service instance with the mesh
    /// </summary>
    Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregister a service instance from the mesh
    /// </summary>
    Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all instances of a service by name
    /// </summary>
    Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific service instance by ID
    /// </summary>
    Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all registered services
    /// </summary>
    Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the health status of a service
    /// </summary>
    Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default);
}

public record ServiceRegistration(
    string ServiceName,
    string ServiceId,
    string Address,
    int Port,
    Dictionary<string, string> Metadata,
    HealthCheckConfiguration? HealthCheck = null);

public record ServiceInstance(
    string ServiceName,
    string ServiceId,
    string Address,
    int Port,
    HealthStatus Status,
    Dictionary<string, string> Metadata,
    DateTime RegisteredAt,
    DateTime LastHealthCheck);

public record ServiceRegistrationResult(
    bool Success,
    string ServiceId,
    string? ErrorMessage = null);

public record HealthCheckConfiguration(
    string Endpoint,
    TimeSpan Interval,
    TimeSpan Timeout,
    int UnhealthyThreshold);

public enum HealthStatus
{
    Unknown,
    Healthy,
    Unhealthy,
    Degraded
}

namespace ToksaMesh.Protocols;

/// <summary>
/// Load balancer interface for distributing requests across service instances
/// </summary>
public interface ILoadBalancer
{
    /// <summary>
    /// Select the next service instance for a request
    /// </summary>
    Task<ServiceInstance?> SelectInstanceAsync(string serviceName, LoadBalancingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Report the result of a request for tracking and circuit breaking
    /// </summary>
    Task ReportResultAsync(string serviceId, RequestResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current load balancing statistics
    /// </summary>
    Task<LoadBalancingStats> GetStatsAsync(string serviceName, CancellationToken cancellationToken = default);
}

public record LoadBalancingContext(
    string? PreferredZone = null,
    Dictionary<string, string>? Headers = null,
    string? SessionId = null);

public record RequestResult(
    string ServiceId,
    bool Success,
    TimeSpan ResponseTime,
    int? StatusCode = null,
    string? ErrorMessage = null);

public record LoadBalancingStats(
    string ServiceName,
    int TotalRequests,
    int SuccessfulRequests,
    int FailedRequests,
    TimeSpan AverageResponseTime,
    Dictionary<string, int> InstanceRequestCounts);

public enum LoadBalancingStrategy
{
    RoundRobin,
    LeastConnections,
    Random,
    WeightedRoundRobin,
    IPHash
}

namespace ToskaMesh.Gateway.Configuration;

/// <summary>
/// Resilience settings applied to outbound proxy calls.
/// </summary>
public class GatewayResilienceOptions
{
    public const string SectionName = "Mesh:Gateway:Resilience";

    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for the first retry attempt.
    /// </summary>
    public double RetryBaseDelayMilliseconds { get; set; } = 200;

    /// <summary>
    /// Exponential backoff multiplier applied per retry.
    /// </summary>
    public double RetryBackoffExponent { get; set; } = 2.0;

    /// <summary>
    /// Maximum random jitter (milliseconds) added to each retry delay.
    /// </summary>
    public double RetryJitterMilliseconds { get; set; } = 200;

    /// <summary>
    /// Failure ratio required to open the circuit breaker (0-1).
    /// </summary>
    public double CircuitBreakerFailureThreshold { get; set; } = 0.5;

    /// <summary>
    /// Sampling window (seconds) used by the circuit breaker.
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum request volume required before circuit breaker evaluates failures.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Duration (seconds) the circuit remains open before attempting recovery.
    /// </summary>
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 20;
}

using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using ToskaMesh.Protocols;

namespace ToskaMesh.Common.Resilience;

/// <summary>
/// Polly-based implementation of <see cref="ICircuitBreaker"/>.
/// </summary>
public class PollyCircuitBreaker : ICircuitBreaker
{
    private readonly ResiliencePipeline _pipeline;
    private readonly CircuitBreakerManualControl _manualControl;
    private readonly ILogger<PollyCircuitBreaker> _logger;
    private readonly object _lock = new();
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    public string Name { get; }
    public CircuitBreakerState State => _state;

    public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;

    public PollyCircuitBreaker(
        string name,
        CircuitBreakerOptions options,
        ILogger<PollyCircuitBreaker> logger)
    {
        Name = name;
        _logger = logger;
        _manualControl = new CircuitBreakerManualControl();

        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureRatio,
                SamplingDuration = options.SamplingDuration,
                MinimumThroughput = options.MinimumThroughput,
                BreakDuration = options.BreakDuration,
                ManualControl = _manualControl,
                OnOpened = args =>
                {
                    TransitionTo(CircuitBreakerState.Open);
                    _logger.LogWarning("Circuit breaker '{Name}' opened due to failures", Name);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    TransitionTo(CircuitBreakerState.Closed);
                    _logger.LogInformation("Circuit breaker '{Name}' closed", Name);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    TransitionTo(CircuitBreakerState.HalfOpen);
                    _logger.LogInformation("Circuit breaker '{Name}' half-opened, testing...", Name);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(async ct => await action(), cancellationToken);
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await _pipeline.ExecuteAsync(async ct =>
        {
            await action();
        }, cancellationToken);
    }

    public void RecordSuccess()
    {
        // Polly handles this automatically through the pipeline
        // This method exists for manual state management if needed
    }

    public void RecordFailure(Exception exception)
    {
        // Polly handles this automatically through the pipeline
        // This method exists for manual state management if needed
        _logger.LogDebug(exception, "Failure recorded for circuit breaker '{Name}'", Name);
    }

    private void TransitionTo(CircuitBreakerState newState)
    {
        CircuitBreakerState previousState;
        lock (_lock)
        {
            previousState = _state;
            if (previousState == newState)
                return;

            _state = newState;
        }

        StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(
            Name,
            previousState,
            newState,
            DateTimeOffset.UtcNow));
    }
}

/// <summary>
/// Configuration options for <see cref="PollyCircuitBreaker"/>.
/// </summary>
public class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";

    /// <summary>
    /// The failure ratio threshold (0.0 to 1.0) at which the circuit opens.
    /// Default: 0.5 (50% failures).
    /// </summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// The duration over which failure ratio is calculated.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum number of requests in the sampling duration before the circuit can open.
    /// Default: 10.
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// How long the circuit stays open before transitioning to half-open.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}

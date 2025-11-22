namespace ToskaMesh.Protocols;

/// <summary>
/// Defines the contract for a circuit breaker that protects downstream dependencies.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Gets the name of the circuit breaker.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current state of the breaker.
    /// </summary>
    CircuitBreakerState State { get; }

    /// <summary>
    /// Executes an asynchronous operation through the breaker.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an asynchronous operation through the breaker.
    /// </summary>
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful execution. Implementations use this to transition from half open to closed.
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Records a failure and lets the breaker decide whether to transition to open.
    /// </summary>
    void RecordFailure(Exception exception);

    /// <summary>
    /// Raised whenever the breaker transitions between states.
    /// </summary>
    event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;
}

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Event payload for circuit breaker state changes.
/// </summary>
/// <param name="BreakerName">Name of the breaker.</param>
/// <param name="PreviousState">State before the transition.</param>
/// <param name="CurrentState">State after the transition.</param>
/// <param name="TriggeredAt">Timestamp when the transition occurred.</param>
public record CircuitBreakerStateChangedEventArgs(
    string BreakerName,
    CircuitBreakerState PreviousState,
    CircuitBreakerState CurrentState,
    DateTimeOffset TriggeredAt);

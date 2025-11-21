namespace ToskaMesh.Common.Messaging;

/// <summary>
/// Base interface for all mesh events.
/// </summary>
public interface IMeshEvent
{
    /// <summary>
    /// Unique event ID.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Correlation ID for tracing related events.
    /// </summary>
    string? CorrelationId { get; }
}

/// <summary>
/// Event published when a service registers with the mesh.
/// </summary>
public record ServiceRegisteredEvent : IMeshEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    public required string ServiceId { get; init; }
    public required string ServiceName { get; init; }
    public required string Address { get; init; }
    public int Port { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Event published when a service deregisters from the mesh.
/// </summary>
public record ServiceDeregisteredEvent : IMeshEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    public required string ServiceId { get; init; }
    public required string ServiceName { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event published when a service health status changes.
/// </summary>
public record ServiceHealthChangedEvent : IMeshEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    public required string ServiceId { get; init; }
    public required string ServiceName { get; init; }
    public required string PreviousStatus { get; init; }
    public required string CurrentStatus { get; init; }
    public string? HealthCheckOutput { get; init; }
}

/// <summary>
/// Event published when configuration changes.
/// </summary>
public record ConfigurationChangedEvent : IMeshEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    public required string ConfigKey { get; init; }
    public string? PreviousValue { get; init; }
    public required string NewValue { get; init; }
    public required string ChangedBy { get; init; }
}

/// <summary>
/// Event published when an alert is triggered.
/// </summary>
public record AlertTriggeredEvent : IMeshEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    public required string AlertName { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public required string Source { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

/// <summary>
/// Command to restart a service.
/// </summary>
public record RestartServiceCommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    public required string ServiceId { get; init; }
    public string? Reason { get; init; }
    public bool Force { get; init; }
}

/// <summary>
/// Response to a command.
/// </summary>
public record CommandResponse
{
    public required Guid CommandId { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
}

namespace ToskaMesh.TracingService.Entities;

/// <summary>
/// Represents a single span captured within a distributed trace.
/// </summary>
public class TraceSpan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string? ParentSpanId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public double DurationMs { get; set; }
    public string Status { get; set; } = "Unset";
    public string? Kind { get; set; }
    public string? CorrelationId { get; set; }
    public double? CpuUsage { get; set; }
    public double? MemoryUsageMb { get; set; }
    public string? AttributesJson { get; set; }
    public string? EventsJson { get; set; }
    public string? ResourceAttributesJson { get; set; }
}

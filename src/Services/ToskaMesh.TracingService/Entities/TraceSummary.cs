namespace ToskaMesh.TracingService.Entities;

/// <summary>
/// Materialized summary of a trace for fast list queries.
/// </summary>
public class TraceSummary
{
    public string TraceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public double DurationMs { get; set; }
    public string Status { get; set; } = "Unset";
    public int SpanCount { get; set; }
    public string? CorrelationId { get; set; }
}

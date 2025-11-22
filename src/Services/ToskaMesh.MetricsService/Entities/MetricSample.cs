using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Entities;

/// <summary>
/// Represents a metric sample that has been persisted for historical queries.
/// </summary>
public class MetricSample
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public MetricType Type { get; set; }
    public double Value { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string>? Labels { get; set; }
    public string? Unit { get; set; }
}

using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Entities;

/// <summary>
/// Stores metadata about a custom metric registered through the API.
/// </summary>
public class CustomMetricDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public MetricType Type { get; set; }
    public string[]? LabelNames { get; set; }
    public string? Unit { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

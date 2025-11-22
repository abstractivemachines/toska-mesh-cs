using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Entities;

public class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public MetricAggregation Aggregation { get; set; } = MetricAggregation.Average;
    public double Threshold { get; set; }
    public AlertComparisonOperator Operator { get; set; } = AlertComparisonOperator.GreaterThan;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);
    public bool Enabled { get; set; } = true;
    public string Severity { get; set; } = "info";
    public string NotificationChannel { get; set; } = "webhook";
    public Dictionary<string, string>? LabelFilters { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

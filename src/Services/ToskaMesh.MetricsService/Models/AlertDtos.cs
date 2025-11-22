using System.ComponentModel.DataAnnotations;

namespace ToskaMesh.MetricsService.Models;

public record AlertRuleDto(
    Guid Id,
    string Name,
    string MetricName,
    MetricAggregation Aggregation,
    double Threshold,
    AlertComparisonOperator Operator,
    TimeSpan Window,
    bool Enabled,
    string Severity,
    string NotificationChannel,
    IReadOnlyDictionary<string, string>? LabelFilters,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? Description);

public record CreateAlertRuleRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Required]
    public string MetricName { get; init; } = string.Empty;

    public MetricAggregation Aggregation { get; init; } = MetricAggregation.Average;

    [Required]
    public double Threshold { get; init; }

    public AlertComparisonOperator Operator { get; init; } = AlertComparisonOperator.GreaterThan;

    public double WindowMinutes { get; init; } = 5;

    public bool Enabled { get; init; } = true;

    public string Severity { get; init; } = "info";

    public string NotificationChannel { get; init; } = "webhook";

    public Dictionary<string, string>? LabelFilters { get; init; }
}

public record UpdateAlertRuleRequest : CreateAlertRuleRequest;

public record AlertEvaluationResult(Guid RuleId, bool Triggered, double ObservedValue, double Threshold, AlertComparisonOperator Operator, DateTimeOffset EvaluatedAt, DateTimeOffset? LastSampleTimestamp);

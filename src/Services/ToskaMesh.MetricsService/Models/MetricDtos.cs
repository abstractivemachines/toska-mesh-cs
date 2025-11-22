using Prometheus;
using System.ComponentModel.DataAnnotations;

namespace ToskaMesh.MetricsService.Models;

public record RegisterCustomMetricRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string HelpText { get; init; } = string.Empty;

    [Required]
    public MetricType Type { get; init; }

    public string[]? LabelNames { get; init; }

    public string? Unit { get; init; }

    public bool IsVisible { get; init; } = true;
}

public record RecordMetricRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public MetricType Type { get; init; }

    [Range(double.MinValue, double.MaxValue)]
    public double Value { get; init; }

    public Dictionary<string, string>? Labels { get; init; }

    public string? Unit { get; init; }
}

public record MetricQuery
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public MetricAggregation Aggregation { get; init; } = MetricAggregation.None;

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public TimeSpan? BucketSize { get; init; }

    public Dictionary<string, string>? LabelFilters { get; init; }

    public int? Limit { get; init; }
}

public record MetricQueryResponse(string Name, IReadOnlyCollection<MetricDataPoint> DataPoints);

public record MetricDataPoint(DateTimeOffset Timestamp, double Value, IReadOnlyDictionary<string, string>? Labels);

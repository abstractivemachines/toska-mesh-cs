using System.ComponentModel.DataAnnotations;

namespace ToskaMesh.TracingService.Models;

public record TraceSpanDto
{
    [Required]
    public string TraceId { get; init; } = string.Empty;

    [Required]
    public string SpanId { get; init; } = string.Empty;

    public string? ParentSpanId { get; init; }

    [Required]
    public string ServiceName { get; init; } = string.Empty;

    [Required]
    public string OperationName { get; init; } = string.Empty;

    public DateTimeOffset StartTime { get; init; }

    public DateTimeOffset EndTime { get; init; }

    public string Status { get; init; } = "Unset";

    public string? Kind { get; init; }

    public string? CorrelationId { get; init; }

    public IReadOnlyDictionary<string, string?>? Attributes { get; init; }

    public IReadOnlyDictionary<string, string?>? Events { get; init; }

    public IReadOnlyDictionary<string, string?>? ResourceAttributes { get; init; }

    public double? CpuUsage { get; init; }

    public double? MemoryUsageMb { get; init; }
}

public record TraceIngestRequest
{
    [Required]
    public IReadOnlyCollection<TraceSpanDto> Spans { get; init; } = Array.Empty<TraceSpanDto>();

    public string? Collector { get; init; }

    public string? CorrelationId { get; init; }
}

public record TraceQueryParameters
{
    public string? ServiceName { get; init; }
    public string? OperationName { get; init; }
    public string? Status { get; init; }
    public string? CorrelationId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public double? MinDurationMs { get; init; }
    public double? MaxDurationMs { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record TraceSummaryDto(
    string TraceId,
    string ServiceName,
    string Operation,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    double DurationMs,
    string Status,
    int SpanCount,
    string? CorrelationId);

public record TraceDetailDto(TraceSummaryDto Summary, IReadOnlyCollection<TraceSpanDto> Spans);

public record TraceQueryResponse(int Total, int Page, int PageSize, IReadOnlyCollection<TraceSummaryDto> Items);

public record TracePerformanceRequest
{
    public string? ServiceName { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? OperationName { get; init; }
}

public record TracePerformanceResponse(
    double AverageDurationMs,
    double P95DurationMs,
    double ErrorRate,
    double ThroughputPerMinute,
    IReadOnlyCollection<ServiceLatencyBreakdown> Services,
    IReadOnlyCollection<OperationHotspot> Hotspots);

public record ServiceLatencyBreakdown(string ServiceName, double AverageDurationMs, double P95DurationMs, double ErrorRate);

public record OperationHotspot(string OperationName, double AverageDurationMs, double ErrorRate);

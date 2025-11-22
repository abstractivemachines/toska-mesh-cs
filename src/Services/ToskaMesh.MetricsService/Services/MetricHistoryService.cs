using Microsoft.EntityFrameworkCore;
using Prometheus;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Entities;
using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Services;

public interface IMetricHistoryService
{
    Task RecordSampleAsync(string name, double value, MetricType type, IDictionary<string, string>? labels, string? unit, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MetricDataPoint>> QueryAsync(MetricQuery query, CancellationToken cancellationToken);
}

public class MetricHistoryService : IMetricHistoryService
{
    private readonly MetricsDbContext _dbContext;
    private const int DefaultLimit = 500;

    public MetricHistoryService(MetricsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordSampleAsync(string name, double value, MetricType type, IDictionary<string, string>? labels, string? unit, CancellationToken cancellationToken)
    {
        var sample = new MetricSample
        {
            Name = name,
            Value = value,
            Type = type,
            Labels = labels is null ? null : new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase),
            Unit = unit,
            TimestampUtc = DateTime.UtcNow
        };

        _dbContext.MetricSamples.Add(sample);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MetricDataPoint>> QueryAsync(MetricQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Name))
        {
            throw new ArgumentException("Metric name is required.", nameof(query));
        }

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, 5_000);

        var metricQuery = _dbContext.MetricSamples.AsNoTracking()
            .Where(sample => sample.Name == query.Name);

        if (query.From is not null)
        {
            var fromUtc = query.From.Value.UtcDateTime;
            metricQuery = metricQuery.Where(sample => sample.TimestampUtc >= fromUtc);
        }

        if (query.To is not null)
        {
            var toUtc = query.To.Value.UtcDateTime;
            metricQuery = metricQuery.Where(sample => sample.TimestampUtc <= toUtc);
        }

        var orderedSamples = await metricQuery
            .OrderByDescending(sample => sample.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var filtered = ApplyLabelFilters(orderedSamples, query.LabelFilters)
            .OrderBy(sample => sample.TimestampUtc)
            .ToList();

        if (filtered.Count == 0)
        {
            return Array.Empty<MetricDataPoint>();
        }

        if (query.Aggregation == MetricAggregation.None)
        {
            return filtered.Select(ToDataPoint).ToList();
        }

        if (query.BucketSize is null || query.BucketSize <= TimeSpan.Zero)
        {
            var aggregateValue = Aggregate(filtered, query.Aggregation);
            return new[]
            {
                new MetricDataPoint(
                    Timestamp: filtered.Last().TimestampUtc,
                    Value: aggregateValue,
                    Labels: filtered.Last().Labels)
            };
        }

        var bucketed = filtered
            .GroupBy(sample => AlignTimestamp(sample.TimestampUtc, query.BucketSize.Value))
            .Select(group => new MetricDataPoint(
                group.Key,
                Aggregate(group, query.Aggregation),
                group.Last().Labels))
            .OrderBy(point => point.Timestamp)
            .ToList();

        return bucketed;
    }

    private static IEnumerable<MetricSample> ApplyLabelFilters(IEnumerable<MetricSample> samples, Dictionary<string, string>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return samples;
        }

        return samples.Where(sample =>
            sample.Labels is not null &&
            filters.All(filter =>
                sample.Labels.TryGetValue(filter.Key, out var value) &&
                string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase)));
    }

    private static double Aggregate(IEnumerable<MetricSample> samples, MetricAggregation aggregation)
    {
        return aggregation switch
        {
            MetricAggregation.Sum => samples.Sum(sample => sample.Value),
            MetricAggregation.Average => samples.Average(sample => sample.Value),
            MetricAggregation.Min => samples.Min(sample => sample.Value),
            MetricAggregation.Max => samples.Max(sample => sample.Value),
            MetricAggregation.Latest => samples.OrderBy(sample => sample.TimestampUtc).Last().Value,
            _ => samples.Sum(sample => sample.Value)
        };
    }

    private static DateTimeOffset AlignTimestamp(DateTime timestampUtc, TimeSpan bucketSize)
    {
        var ticks = bucketSize.Ticks;
        if (ticks <= 0)
        {
            return timestampUtc;
        }

        var bucketIndex = timestampUtc.Ticks / ticks;
        var alignedTicks = bucketIndex * ticks;
        return new DateTimeOffset(new DateTime(alignedTicks, DateTimeKind.Utc));
    }

    private static MetricDataPoint ToDataPoint(MetricSample sample) =>
        new(DateTime.SpecifyKind(sample.TimestampUtc, DateTimeKind.Utc), sample.Value, sample.Labels);
}

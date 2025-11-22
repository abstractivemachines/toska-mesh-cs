using Microsoft.EntityFrameworkCore;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Models;

namespace ToskaMesh.TracingService.Services;

public interface ITraceAnalyticsService
{
    Task<TracePerformanceResponse> GetPerformanceAsync(TracePerformanceRequest request, CancellationToken cancellationToken);
}

public class TraceAnalyticsService : ITraceAnalyticsService
{
    private readonly TracingDbContext _dbContext;

    public TraceAnalyticsService(TracingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TracePerformanceResponse> GetPerformanceAsync(TracePerformanceRequest request, CancellationToken cancellationToken)
    {
        var query = _dbContext.TraceSpans.AsNoTracking()
            .Where(span => span.ParentSpanId == null);

        if (!string.IsNullOrWhiteSpace(request.ServiceName))
        {
            query = query.Where(span => span.ServiceName == request.ServiceName);
        }

        if (!string.IsNullOrWhiteSpace(request.OperationName))
        {
            query = query.Where(span => span.OperationName == request.OperationName);
        }

        DateTime startWindow;
        if (request.From is not null)
        {
            var fromUtc = request.From.Value.UtcDateTime;
            query = query.Where(span => span.StartTimeUtc >= fromUtc);
            startWindow = fromUtc;
        }
        else
        {
            startWindow = DateTime.UtcNow.AddHours(-1);
        }

        DateTime endWindow;
        if (request.To is not null)
        {
            var toUtc = request.To.Value.UtcDateTime;
            query = query.Where(span => span.EndTimeUtc <= toUtc);
            endWindow = toUtc;
        }
        else
        {
            endWindow = DateTime.UtcNow;
        }

        var spans = await query.ToListAsync(cancellationToken);
        if (spans.Count == 0)
        {
            return new TracePerformanceResponse(0, 0, 0, 0, Array.Empty<ServiceLatencyBreakdown>(), Array.Empty<OperationHotspot>());
        }

        var durations = spans.Select(span => span.DurationMs).OrderBy(value => value).ToList();
        var averageDuration = durations.Average();
        var p95 = CalculatePercentile(durations, 0.95);

        var errorCount = spans.Count(span => !string.Equals(span.Status, "Ok", StringComparison.OrdinalIgnoreCase));
        var errorRate = spans.Count == 0 ? 0 : errorCount / (double)spans.Count;

        var totalMinutes = Math.Max((endWindow - startWindow).TotalMinutes, 1);
        var throughput = spans.Count / totalMinutes;

        var serviceBreakdown = spans
            .GroupBy(span => span.ServiceName)
            .Select(group =>
            {
                var groupDurations = group.Select(span => span.DurationMs).OrderBy(value => value).ToList();
                var average = groupDurations.Average();
                var serviceP95 = CalculatePercentile(groupDurations, 0.95);
                var serviceErrors = group.Count(span => !string.Equals(span.Status, "Ok", StringComparison.OrdinalIgnoreCase));
                return new ServiceLatencyBreakdown(group.Key, average, serviceP95, serviceErrors / (double)group.Count());
            })
            .OrderByDescending(item => item.AverageDurationMs)
            .ToList();

        var hotspots = spans
            .GroupBy(span => span.OperationName)
            .Select(group =>
            {
                var avg = group.Average(span => span.DurationMs);
                var failures = group.Count(span => !string.Equals(span.Status, "Ok", StringComparison.OrdinalIgnoreCase));
                return new OperationHotspot(group.Key, avg, failures / (double)group.Count());
            })
            .OrderByDescending(item => item.AverageDurationMs)
            .Take(10)
            .ToList();

        return new TracePerformanceResponse(
            averageDuration,
            p95,
            errorRate,
            throughput,
            serviceBreakdown,
            hotspots);
    }

    private static double CalculatePercentile(IReadOnlyList<double> orderedDurations, double percentile)
    {
        if (orderedDurations.Count == 0)
        {
            return 0;
        }

        if (orderedDurations.Count == 1)
        {
            return orderedDurations[0];
        }

        var position = percentile * (orderedDurations.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return orderedDurations[lowerIndex];
        }

        var weight = position - lowerIndex;
        return orderedDurations[lowerIndex] + (orderedDurations[upperIndex] - orderedDurations[lowerIndex]) * weight;
    }
}

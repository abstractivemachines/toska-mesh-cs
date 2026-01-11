using ToskaMesh.ObservabilityService.Models;

namespace ToskaMesh.ObservabilityService.Services;

public sealed class TopologyGraphBuilder
{
    public TopologyGraph Build(IReadOnlyCollection<SpanRecord> spans)
    {
        var nodes = BuildNodes(spans);
        var edges = spans
            .GroupBy(span => new SpanKey(span.Service, span.Dependency), SpanKeyComparer.Instance)
            .Select(group =>
            {
                var traffic = group.Count();
                var errorRate = traffic == 0 ? 0 : group.Count(span => span.IsError) / (double)traffic;
                var avgLatency = traffic == 0 ? 0 : group.Average(span => span.DurationMs);
                return new TopologyEdge(
                    group.Key.Service,
                    group.Key.Dependency,
                    traffic,
                    Math.Round(avgLatency, 2),
                    Math.Round(errorRate, 4));
            })
            .OrderByDescending(edge => edge.Traffic)
            .ToList();

        return new TopologyGraph(DateTimeOffset.UtcNow, nodes, edges);
    }

    private static IReadOnlyList<TopologyNode> BuildNodes(IReadOnlyCollection<SpanRecord> spans)
    {
        var services = spans
            .SelectMany(span => new[] { span.Service, span.Dependency })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return services
            .Select(service =>
            {
                var serviceSpans = spans.Where(span => string.Equals(span.Service, service, StringComparison.OrdinalIgnoreCase)).ToList();
                var traffic = serviceSpans.Count;
                var errorRate = traffic == 0 ? 0 : serviceSpans.Count(span => span.IsError) / (double)traffic;
                var p95 = CalculatePercentile(serviceSpans.Select(span => span.DurationMs).ToList(), 0.95);
                var alerts = BuildAlerts(errorRate, p95);

                return new TopologyNode(
                    service,
                    service,
                    traffic,
                    Math.Round(errorRate, 4),
                    Math.Round(p95, 2),
                    alerts);
            })
            .OrderByDescending(node => node.Traffic)
            .ToList();
    }

    private static IReadOnlyList<string> BuildAlerts(double errorRate, double p95Latency)
    {
        var alerts = new List<string>();
        if (errorRate >= 0.05)
        {
            alerts.Add("error-rate");
        }

        if (p95Latency >= 500)
        {
            alerts.Add("latency");
        }

        return alerts;
    }

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var rank = (int)Math.Ceiling(percentile * ordered.Count) - 1;
        rank = Math.Clamp(rank, 0, ordered.Count - 1);
        return ordered[rank];
    }

    private sealed record SpanKey(string Service, string Dependency);

    private sealed class SpanKeyComparer : IEqualityComparer<SpanKey>
    {
        public static SpanKeyComparer Instance { get; } = new();

        public bool Equals(SpanKey? x, SpanKey? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return string.Equals(x.Service, y.Service, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Dependency, y.Dependency, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(SpanKey obj)
        {
            return HashCode.Combine(
                obj.Service.ToLowerInvariant(),
                obj.Dependency.ToLowerInvariant());
        }
    }
}

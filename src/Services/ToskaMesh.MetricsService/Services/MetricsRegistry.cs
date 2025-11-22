using Prometheus;
using System.Collections.Concurrent;
using ToskaMesh.MetricsService.Entities;
using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Services;

public interface IMetricsRegistry
{
    Counter RequestsTotal { get; }
    Counter ErrorsTotal { get; }
    Histogram RequestDuration { get; }
    Gauge ActiveRequests { get; }
    void RegisterCustomMetric(CustomMetricDefinition definition);
    void RecordCustomMetric(CustomMetricDefinition definition, double value, IReadOnlyDictionary<string, string>? labels);
}

public class MetricsRegistry : IMetricsRegistry
{
    private readonly ConcurrentDictionary<string, CustomCollector> _customCollectors = new(StringComparer.OrdinalIgnoreCase);

    public MetricsRegistry()
    {
        RequestsTotal = Metrics.CreateCounter("mesh_requests_total", "Total number of mesh requests");
        ErrorsTotal = Metrics.CreateCounter("mesh_errors_total", "Total number of mesh errors");
        RequestDuration = Metrics.CreateHistogram("mesh_request_duration_seconds", "Mesh request duration (seconds)", new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.01, width: 0.05, count: 20)
        });
        ActiveRequests = Metrics.CreateGauge("mesh_active_requests", "Number of active mesh requests");
    }

    public Counter RequestsTotal { get; }
    public Counter ErrorsTotal { get; }
    public Histogram RequestDuration { get; }
    public Gauge ActiveRequests { get; }

    public void RegisterCustomMetric(CustomMetricDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _customCollectors.GetOrAdd(definition.Name, _ => CreateCollector(definition));
    }

    public void RecordCustomMetric(CustomMetricDefinition definition, double value, IReadOnlyDictionary<string, string>? labels)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_customCollectors.TryGetValue(definition.Name, out var collector))
        {
            collector = CreateCollector(definition);
            _customCollectors.TryAdd(definition.Name, collector);
        }

        if (collector.LabelNames.Length > 0)
        {
            if (labels is null)
            {
                throw new InvalidOperationException($"Metric '{definition.Name}' expects labels: {string.Join(",", collector.LabelNames)}");
            }

            var orderedLabels = collector.LabelNames.Select(label =>
            {
                if (!labels.TryGetValue(label, out var value))
                {
                    throw new InvalidOperationException($"Metric '{definition.Name}' is missing label '{label}'.");
                }
                return value;
            }).ToArray();

            collector.Record(value, orderedLabels);
        }
        else
        {
            collector.Record(value, Array.Empty<string>());
        }
    }

    private CustomCollector CreateCollector(CustomMetricDefinition definition)
    {
        var labelNames = definition.LabelNames ?? Array.Empty<string>();

        return definition.Type switch
        {
            MetricType.Counter => CustomCollector.Counter(Metrics.CreateCounter(definition.Name, definition.HelpText, new CounterConfiguration
            {
                LabelNames = labelNames
            }), labelNames),
            MetricType.Gauge => CustomCollector.Gauge(Metrics.CreateGauge(definition.Name, definition.HelpText, new GaugeConfiguration
            {
                LabelNames = labelNames
            }), labelNames),
            MetricType.Histogram => CustomCollector.Histogram(Metrics.CreateHistogram(definition.Name, definition.HelpText, new HistogramConfiguration
            {
                LabelNames = labelNames
            }), labelNames),
            MetricType.Summary => CustomCollector.Summary(Metrics.CreateSummary(definition.Name, definition.HelpText, new SummaryConfiguration
            {
                LabelNames = labelNames
            }), labelNames),
            _ => throw new ArgumentOutOfRangeException(nameof(definition.Type), $"Unsupported metric type '{definition.Type}'.")
        };
    }

    private sealed class CustomCollector
    {
        private readonly Counter? _counter;
        private readonly Gauge? _gauge;
        private readonly Histogram? _histogram;
        private readonly Summary? _summary;
        public string[] LabelNames { get; }

        private CustomCollector(
            string[] labelNames,
            Counter? counter,
            Gauge? gauge,
            Histogram? histogram,
            Summary? summary)
        {
            LabelNames = labelNames;
            _counter = counter;
            _gauge = gauge;
            _histogram = histogram;
            _summary = summary;
        }

        public static CustomCollector Counter(Counter counter, string[] labelNames) =>
            new(labelNames, counter, null, null, null);

        public static CustomCollector Gauge(Gauge gauge, string[] labelNames) =>
            new(labelNames, null, gauge, null, null);

        public static CustomCollector Histogram(Histogram histogram, string[] labelNames) =>
            new(labelNames, null, null, histogram, null);

        public static CustomCollector Summary(Summary summary, string[] labelNames) =>
            new(labelNames, null, null, null, summary);

        public void Record(double value, string[] labelValues)
        {
            if (_counter is not null)
            {
                if (value < 0)
                {
                    throw new InvalidOperationException("Counters cannot be decreased.");
                }

                _counter.WithLabels(labelValues).Inc(value);
                return;
            }

            if (_gauge is not null)
            {
                _gauge.WithLabels(labelValues).Set(value);
                return;
            }

            if (_histogram is not null)
            {
                _histogram.WithLabels(labelValues).Observe(value);
                return;
            }

            if (_summary is not null)
            {
                _summary.WithLabels(labelValues).Observe(value);
            }
        }
    }
}

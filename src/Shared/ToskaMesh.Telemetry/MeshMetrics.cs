using System.Diagnostics.Metrics;

namespace ToskaMesh.Telemetry;

/// <summary>
/// Custom metrics for Toska Mesh
/// </summary>
public class MeshMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _errorCounter;
    private readonly ObservableGauge<int> _activeConnections;

    public MeshMetrics(string serviceName)
    {
        _meter = new Meter($"ToskaMesh.{serviceName}", "1.0.0");

        _requestCounter = _meter.CreateCounter<long>(
            "mesh.requests.total",
            description: "Total number of requests processed");

        _requestDuration = _meter.CreateHistogram<double>(
            "mesh.request.duration",
            unit: "ms",
            description: "Request duration in milliseconds");

        _errorCounter = _meter.CreateCounter<long>(
            "mesh.errors.total",
            description: "Total number of errors");

        _activeConnections = _meter.CreateObservableGauge<int>(
            "mesh.connections.active",
            () => GetActiveConnectionCount(),
            description: "Number of active connections");
    }

    public void RecordRequest(string endpoint, string method)
    {
        _requestCounter.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint),
                                 new KeyValuePair<string, object?>("method", method));
    }

    public void RecordRequestDuration(double durationMs, string endpoint, int statusCode)
    {
        _requestDuration.Record(durationMs,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("status_code", statusCode));
    }

    public void RecordError(string errorType, string? endpoint = null)
    {
        _errorCounter.Add(1,
            new KeyValuePair<string, object?>("error_type", errorType),
            new KeyValuePair<string, object?>("endpoint", endpoint ?? "unknown"));
    }

    private int GetActiveConnectionCount()
    {
        // This should be implemented to track actual active connections
        // For now, return 0 as a placeholder
        return 0;
    }
}

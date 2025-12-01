using System.Diagnostics.Metrics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.Telemetry.Consumption;

namespace ToskaMesh.Gateway.Telemetry;

/// <summary>
/// Captures YARP forwarding metrics and publishes them via OpenTelemetry/Prometheus.
/// </summary>
public class GatewayProxyMetricsConsumer : IForwarderTelemetryConsumer
{
    private readonly Counter<long> _forwardedRequests;
    private readonly Counter<long> _failedRequests;
    private readonly UpDownCounter<long> _inflightRequests;

    public GatewayProxyMetricsConsumer(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ToskaMesh.Gateway.Proxy", "1.0.0");
        _forwardedRequests = meter.CreateCounter<long>(
            "gateway.proxy.requests",
            description: "Total requests forwarded by the gateway");
        _failedRequests = meter.CreateCounter<long>(
            "gateway.proxy.failures",
            description: "Requests that failed before reaching an upstream destination");
        _inflightRequests = meter.CreateUpDownCounter<long>(
            "gateway.proxy.inflight",
            description: "In-flight proxy requests currently being processed");
    }

    public void OnForwarderStart(DateTime timestamp, string destinationPrefix)
    {
        _forwardedRequests.Add(1, new KeyValuePair<string, object?>("destination", destinationPrefix));
        _inflightRequests.Add(1);
    }

    public void OnForwarderStop(DateTime timestamp, int statusCode)
    {
        _inflightRequests.Add(-1, new KeyValuePair<string, object?>("status_code", statusCode));
    }

    public void OnForwarderFailed(DateTime timestamp, ForwarderError error)
    {
        _failedRequests.Add(1, new KeyValuePair<string, object?>("error", error.ToString()));
    }

    public void OnForwarderStage(DateTime timestamp, ForwarderStage stage)
    {
        // No-op: stages are not aggregated today.
    }

    public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime)
    {
        // No-op
    }

    public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstRead)
    {
        // No-op
    }

    public void OnForwarderInvoke(DateTime timestamp, string destinationPrefix, string clusterId, string routeId)
    {
        // No-op
    }
}

using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace ToskaMesh.ObservabilityService.Services;

public sealed class ObservabilityMetrics
{
    private readonly Counter<long> requestCounter;
    private readonly Counter<long> errorCounter;
    private readonly Histogram<double> latencyHistogram;

    public ObservabilityMetrics(string serviceName)
    {
        var meter = new Meter($"ToskaMesh.{serviceName}");
        requestCounter = meter.CreateCounter<long>("observability_requests_total");
        errorCounter = meter.CreateCounter<long>("observability_request_errors_total");
        latencyHistogram = meter.CreateHistogram<double>("observability_request_latency_ms");
        meter.CreateObservableGauge("observability_saturation_ratio", () =>
            new Measurement<double>(GetSaturationRatio()));
    }

    public void RecordRequest(HttpContext context, TimeSpan elapsed)
    {
        var route = context.GetEndpoint()?.DisplayName
            ?? context.Request.Path.Value
            ?? "unknown";
        var tags = new KeyValuePair<string, object?>[]
        {
            new("route", route),
            new("method", context.Request.Method),
            new("status_code", context.Response.StatusCode)
        };

        requestCounter.Add(1, tags);
        latencyHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            errorCounter.Add(1, tags);
        }
    }

    private static double GetSaturationRatio()
    {
        var info = GC.GetGCMemoryInfo();
        if (info.TotalAvailableMemoryBytes <= 0)
        {
            return 0;
        }

        var ratio = (double)info.HeapSizeBytes / info.TotalAvailableMemoryBytes;
        return Math.Clamp(ratio, 0, 1);
    }
}

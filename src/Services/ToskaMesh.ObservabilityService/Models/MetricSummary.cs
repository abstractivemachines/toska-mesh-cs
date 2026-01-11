namespace ToskaMesh.ObservabilityService.Models;

public sealed record MetricSummary(
    string Service,
    double RequestRatePerSecond,
    double ErrorRate,
    double P95LatencyMs,
    double Saturation,
    DateTimeOffset CapturedAt);

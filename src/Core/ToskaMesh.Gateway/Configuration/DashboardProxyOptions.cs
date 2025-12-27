namespace ToskaMesh.Gateway.Configuration;

public class DashboardProxyOptions
{
    public const string SectionName = "Dashboard";

    public string PrometheusBaseUrl { get; set; } = "http://prometheus:9090";
    public string TracingBaseUrl { get; set; } = "http://tracing-service:80";
    public string DiscoveryBaseUrl { get; set; } = "http://discovery:80";
    public string HealthMonitorBaseUrl { get; set; } = "http://health-monitor:80";
}

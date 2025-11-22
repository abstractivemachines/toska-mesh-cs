namespace ToskaMesh.HealthMonitor.Configuration;

/// <summary>
/// Configuration for the active health probing service.
/// </summary>
public class HealthMonitorOptions
{
    public const string SectionName = "HealthMonitor";

    public int ProbeIntervalSeconds { get; set; } = 30;
    public int HttpTimeoutSeconds { get; set; } = 5;
    public int TcpTimeoutSeconds { get; set; } = 3;
    public int FailureThreshold { get; set; } = 3;
    public int RecoveryThreshold { get; set; } = 2;
    public string[] HttpHeaders { get; set; } = Array.Empty<string>();
}

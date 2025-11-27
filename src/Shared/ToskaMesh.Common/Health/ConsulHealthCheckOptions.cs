namespace ToskaMesh.Common.Health;

/// <summary>
/// Configuration options for Consul health checks.
/// </summary>
public class ConsulHealthCheckOptions
{
    public const string SectionName = "HealthChecks:Consul";

    public string HostName { get; set; } = "consul";
    public int Port { get; set; } = 8500;
}

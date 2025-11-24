using ToskaMesh.Protocols;

namespace ToskaMesh.Runtime;

/// <summary>
/// Typed routing metadata that will be applied to service registration.
/// </summary>
public class MeshRoutingOptions
{
    public string Scheme { get; set; } = "http";
    public string HealthCheckEndpoint { get; set; } = "/health";
    public LoadBalancingStrategy Strategy { get; set; } = LoadBalancingStrategy.RoundRobin;
    public int Weight { get; set; } = 1;
}

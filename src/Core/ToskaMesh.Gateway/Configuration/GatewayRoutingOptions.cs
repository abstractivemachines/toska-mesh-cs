namespace ToskaMesh.Gateway.Configuration;

public class GatewayRoutingOptions
{
    public const string SectionName = "Mesh:Gateway:Routing";

    /// <summary>
    /// Prefix for routed paths (default: "/api/"). Use "/" to route from the root.
    /// </summary>
    public string RoutePrefix { get; set; } = "/api/";
}

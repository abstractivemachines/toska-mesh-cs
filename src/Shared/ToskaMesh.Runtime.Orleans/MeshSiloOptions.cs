namespace ToskaMesh.Runtime.Orleans;

/// <summary>
/// Options for hosting an Orleans silo with Toska Mesh defaults.
/// </summary>
public class MeshSiloOptions
{
    public string ServiceName { get; set; } = "mesh-silo";
    public string? ServiceId { get; set; }
    public string ClusterId { get; set; } = "toska-mesh";
    public int SiloPort { get; set; } = 11111;
    public int GatewayPort { get; set; } = 30000;
    public string ClusteringMode { get; set; } = "localhost";
    public string? ConsulAddress { get; set; } = "http://localhost:8500";
    public string? ConsulToken { get; set; }
    public string? DatabaseConnectionString { get; set; }
    public string? DatabaseInvariant { get; set; } = "Npgsql";
    public string? AzureStorageConnectionString { get; set; }
    public bool EnableDashboard { get; set; }
    public int DashboardPort { get; set; } = 8080;
}

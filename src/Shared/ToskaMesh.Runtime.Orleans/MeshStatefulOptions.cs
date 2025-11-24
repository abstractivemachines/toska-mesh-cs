namespace ToskaMesh.Runtime.Orleans;

/// <summary>
/// Options for hosting a stateful mesh service without exposing Orleans terminology.
/// </summary>
public class MeshStatefulOptions
{
    public string ServiceName { get; set; } = "mesh-stateful-service";
    public string? ServiceId { get; set; }

    /// <summary>
    /// Cluster identifier.
    /// </summary>
    public string ClusterId { get; set; } = "toska-mesh";

    /// <summary>
    /// Internal host port for cluster communication (maps to Orleans silo port).
    /// </summary>
    public int PrimaryPort { get; set; } = 11111;

    /// <summary>
    /// Client-facing port (maps to Orleans gateway port).
    /// </summary>
    public int ClientPort { get; set; } = 30000;

    public StatefulClusterProvider ClusterProvider { get; set; } = StatefulClusterProvider.Local;

    public string? ConsulAddress { get; set; } = "http://localhost:8500";
    public string? ConsulToken { get; set; }
    public string? DatabaseConnectionString { get; set; }
    public string? DatabaseInvariant { get; set; } = "Npgsql";
    public string? AzureStorageConnectionString { get; set; }
    public bool EnableDashboard { get; set; }
    public int DashboardPort { get; set; } = 8080;

#pragma warning disable CS0618
    /// <summary>
    /// Legacy Orleans terminology (silo port). Prefer PrimaryPort.
    /// </summary>
    [Obsolete("Use PrimaryPort instead.")]
    public int SiloPort
    {
        get => PrimaryPort;
        set => PrimaryPort = value;
    }

    /// <summary>
    /// Legacy Orleans terminology (gateway port). Prefer ClientPort.
    /// </summary>
    [Obsolete("Use ClientPort instead.")]
    public int GatewayPort
    {
        get => ClientPort;
        set => ClientPort = value;
    }

    /// <summary>
    /// Legacy string clustering mode. Prefer ClusterProvider enum.
    /// </summary>
    [Obsolete("Use ClusterProvider instead.")]
    public string ClusteringMode
    {
        get => ClusterProvider.ToString().ToLowerInvariant();
        set
        {
            if (Enum.TryParse<StatefulClusterProvider>(value, true, out var parsed))
            {
                ClusterProvider = parsed;
            }
        }
    }
#pragma warning restore CS0618
}

public enum StatefulClusterProvider
{
    Local,
    Consul,
    AzureTable,
    AdoNet
}

namespace ToskaMesh.Core.Configuration;

/// <summary>
/// Configuration options for Orleans clustering.
/// </summary>
public class OrleansClusterConfig
{
    /// <summary>
    /// The cluster ID. All silos in the same cluster must have the same cluster ID.
    /// </summary>
    public string ClusterId { get; set; } = "toska-mesh";

    /// <summary>
    /// The service ID. This identifies the application/service type.
    /// </summary>
    public string ServiceId { get; set; } = "toska-mesh-service";

    /// <summary>
    /// The silo port for Orleans internal communication.
    /// </summary>
    public int SiloPort { get; set; } = 11111;

    /// <summary>
    /// The gateway port for client-to-silo communication.
    /// </summary>
    public int GatewayPort { get; set; } = 30000;

    /// <summary>
    /// The clustering provider.
    /// </summary>
    public OrleansClusterProvider ClusterProvider { get; set; } = OrleansClusterProvider.Localhost;

    /// <summary>
    /// Legacy string clustering mode. Prefer ClusterProvider.
    /// </summary>
    [Obsolete("Use ClusterProvider instead.")]
    public string ClusteringMode
    {
        get => ClusterProvider.ToString().ToLowerInvariant();
        set
        {
            if (Enum.TryParse<OrleansClusterProvider>(value, true, out var parsed))
            {
                ClusterProvider = parsed;
            }
        }
    }

    /// <summary>
    /// Consul address for service discovery (when using Consul clustering).
    /// </summary>
    public string? ConsulAddress { get; set; } = "http://localhost:8500";

    /// <summary>
    /// Optional Consul ACL token (when required by the cluster).
    /// </summary>
    public string? ConsulToken { get; set; }

    /// <summary>
    /// Azure Storage connection string (when using Azure Table clustering).
    /// </summary>
    public string? AzureStorageConnectionString { get; set; }

    /// <summary>
    /// Database connection string (for ADO.NET clustering and storage).
    /// </summary>
    public string? DatabaseConnectionString { get; set; }

    /// <summary>
    /// Database invariant name (e.g., "Npgsql" for PostgreSQL).
    /// </summary>
    public string? DatabaseInvariant { get; set; } = "Npgsql";

    /// <summary>
    /// Enable Orleans dashboard for monitoring (development only).
    /// </summary>
    public bool EnableDashboard { get; set; } = false;

    /// <summary>
    /// Dashboard port (if enabled).
    /// </summary>
    public int DashboardPort { get; set; } = 8080;
}

public enum OrleansClusterProvider
{
    Localhost,
    Consul,
    AzureTable,
    AdoNet
}

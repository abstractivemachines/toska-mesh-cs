using Microsoft.Extensions.Configuration;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Protocols;

namespace ToskaMesh.Runtime;

/// <summary>
/// Options describing a mesh-aware service instance.
/// </summary>
public class MeshServiceOptions
{
    public string ServiceName { get; set; } = "mesh-service";
    public string? ServiceId { get; set; }
    public string Address { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public string HealthEndpoint { get; set; } = "/health";
    public TimeSpan HealthInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HealthTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public int UnhealthyThreshold { get; set; } = 3;
    public bool HeartbeatEnabled { get; set; } = true;
    public bool EnableTelemetry { get; set; } = true;
    public bool EnableAuth { get; set; } = true;
    public bool RegisterAutomatically { get; set; } = true;
    public bool AllowNoopServiceRegistry { get; set; } = false;
    public ServiceRegistryProvider ServiceRegistryProvider { get; set; } = ServiceRegistryProvider.Grpc;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(ServiceId))
        {
            ServiceId = $"{ServiceName}-{Guid.NewGuid():N}";
        }

        if (!Metadata.ContainsKey("health_check_endpoint"))
        {
            Metadata["health_check_endpoint"] = HealthEndpoint;
        }

        if (!Metadata.ContainsKey("scheme"))
        {
            Metadata["scheme"] = "http";
        }

        if (Port <= 0)
        {
            Port = 8080;
        }

        Validate();
    }

    public ServiceRegistration ToRegistration()
    {
        EnsureDefaults();
        return new ServiceRegistration(
            ServiceName,
            ServiceId!,
            Address,
            Port,
            new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase),
            new HealthCheckConfiguration(
                HealthEndpoint,
                HealthInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : HealthInterval,
                HealthTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : HealthTimeout,
                UnhealthyThreshold <= 0 ? 3 : UnhealthyThreshold));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("MeshServiceOptions.ServiceName is required.");
        }

        if (Port < 0)
        {
            throw new InvalidOperationException("MeshServiceOptions.Port must be >= 0.");
        }

        if (HealthInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MeshServiceOptions.HealthInterval must be greater than zero.");
        }

        if (HealthTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MeshServiceOptions.HealthTimeout must be greater than zero.");
        }
    }

    public static MeshServiceOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new MeshServiceOptions();
        configuration.GetSection("Mesh:Service").Bind(options);
        options.EnsureDefaults();
        return options;
    }
}

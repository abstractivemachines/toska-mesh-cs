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
    public string Address { get; set; } = "0.0.0.0";
    public string? AdvertisedAddress { get; set; }
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
    public MeshRoutingOptions Routing { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(ServiceId))
        {
            ServiceId = $"{ServiceName}-{Guid.NewGuid():N}";
        }

        // Port=0 is allowed for ephemeral ports in tests.

        Address = string.IsNullOrWhiteSpace(Address) ? "0.0.0.0" : Address;
        AdvertisedAddress = string.IsNullOrWhiteSpace(AdvertisedAddress) ? Address : AdvertisedAddress;

        Validate();
    }

    public ServiceRegistration ToRegistration()
    {
        EnsureDefaults();
        return new ServiceRegistration(
            ServiceName,
            ServiceId!,
            AdvertisedAddress!,
            Port,
            BuildMetadata(),
            new HealthCheckConfiguration(
                Routing.HealthCheckEndpoint,
                HealthInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : HealthInterval,
                HealthTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : HealthTimeout,
                UnhealthyThreshold <= 0 ? 3 : UnhealthyThreshold));
    }

    private Dictionary<string, string> BuildMetadata()
    {
        var meta = new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase);
        meta["scheme"] = Routing.Scheme;
        meta["health_check_endpoint"] = Routing.HealthCheckEndpoint;
        meta["lb_strategy"] = Routing.Strategy.ToString();
        if (Routing.Weight > 0)
        {
            meta["weight"] = Routing.Weight.ToString();
        }
        return meta;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("MeshServiceOptions.ServiceName is required.");
        }

        if (string.IsNullOrWhiteSpace(AdvertisedAddress))
        {
            throw new InvalidOperationException("MeshServiceOptions.AdvertisedAddress is required.");
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

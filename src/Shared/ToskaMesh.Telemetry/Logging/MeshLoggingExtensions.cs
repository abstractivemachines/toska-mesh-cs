using Serilog;

namespace ToskaMesh.Telemetry.Logging;

/// <summary>
/// Helper extensions for standard Toska Mesh logging enrichment.
/// </summary>
public static class MeshLoggingExtensions
{
    /// <summary>
    /// Adds the default Toska Mesh enrichers (service metadata + correlation id).
    /// </summary>
    public static LoggerConfiguration AddMeshEnrichers(
        this LoggerConfiguration loggerConfiguration,
        string serviceName)
    {
        return loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.With(new ServiceNameEnricher(serviceName))
            .Enrich.With<CorrelationIdEnricher>();
    }
}

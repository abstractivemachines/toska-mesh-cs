using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ToksaMesh.Telemetry;

/// <summary>
/// Extension methods for configuring OpenTelemetry
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Add OpenTelemetry with Prometheus and distributed tracing
    /// </summary>
    public static IServiceCollection AddMeshTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(serviceName)
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        return services;
    }

    /// <summary>
    /// Add structured logging with Serilog
    /// </summary>
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services)
    {
        // Serilog configuration will be added in Program.cs
        // This method is a placeholder for additional logging configuration
        return services;
    }
}

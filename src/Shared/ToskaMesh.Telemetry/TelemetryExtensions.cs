using System;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ToskaMesh.Telemetry.Tracing;
using ToskaMesh.Security;

namespace ToskaMesh.Telemetry;

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
        IConfiguration configuration,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        var telemetryOptions = configuration.GetSection(MeshTelemetryOptions.SectionName).Get<MeshTelemetryOptions>()
            ?? new MeshTelemetryOptions();

        var activitySource = MeshActivitySource.Get(serviceName, serviceVersion);
        services.AddSingleton(activitySource);

        if (telemetryOptions.TracingIngest.Enabled)
        {
            if (telemetryOptions.TracingIngest.UseMeshServiceAuth)
            {
                services.AddMeshServiceIdentity(configuration);
            }

            var exporterOptions = new TracingIngestExporterOptions(serviceName, serviceVersion, telemetryOptions.TracingIngest);
            services.AddSingleton(exporterOptions);
            services.AddHttpClient(TracingIngestExporter.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, exporterOptions.ExportTimeoutSeconds));
            });
            services.AddSingleton<TracingIngestExporter>();
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(CreateSampler(telemetryOptions.Sampling))
                    .AddSource(activitySource.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (telemetryOptions.EnableConsoleTraceExporter)
                {
                    tracing.AddConsoleExporter();
                }

                if (telemetryOptions.TracingIngest.Enabled)
                {
                    tracing.AddProcessor(sp =>
                    {
                        var exporter = sp.GetRequiredService<TracingIngestExporter>();
                        var exporterOptions = sp.GetRequiredService<TracingIngestExporterOptions>();
                        var queueSize = Math.Max(1, exporterOptions.QueueSize);
                        var batchSize = Math.Max(1, exporterOptions.BatchSize);
                        var delayMs = Math.Max(1, exporterOptions.ExportDelayMs);
                        var timeoutMs = Math.Max(1, exporterOptions.ExportTimeoutSeconds) * 1000;

                        return new BatchActivityExportProcessor(
                            exporter,
                            queueSize,
                            delayMs,
                            timeoutMs,
                            batchSize);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                metrics.AddMeter($"ToskaMesh.{serviceName}");

                if (string.Equals(serviceName, "Gateway", StringComparison.OrdinalIgnoreCase))
                {
                    metrics.AddMeter("ToskaMesh.Gateway.Proxy");
                }
            });

        return services;
    }

    public static IServiceCollection AddMeshTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        return services.AddMeshTelemetry(new ConfigurationBuilder().Build(), serviceName, serviceVersion);
    }

    /// <summary>
    /// Registers an activity source without configuring the full OpenTelemetry pipeline.
    /// </summary>
    public static IServiceCollection AddMeshTracing(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        services.AddSingleton(MeshActivitySource.Get(serviceName, serviceVersion));
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

    private static Sampler CreateSampler(TracingSamplingOptions? options)
    {
        if (options == null)
        {
            return new ParentBasedSampler(new AlwaysOnSampler());
        }

        var ratio = Math.Clamp(options.Ratio, 0.0, 1.0);
        var strategy = options.Strategy?.Trim();
        if (string.IsNullOrWhiteSpace(strategy))
        {
            return new ParentBasedSampler(new AlwaysOnSampler());
        }

        var normalized = new string(strategy.Where(char.IsLetterOrDigit).ToArray())
            .ToLowerInvariant();

        return normalized switch
        {
            "alwayson" => new AlwaysOnSampler(),
            "alwaysoff" => new AlwaysOffSampler(),
            "traceidratiobased" => new TraceIdRatioBasedSampler(ratio),
            "parentbasedalwayson" => new ParentBasedSampler(new AlwaysOnSampler()),
            "parentbasedalwaysoff" => new ParentBasedSampler(new AlwaysOffSampler()),
            "parentbasedtraceidratiobased" => new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)),
            _ => new ParentBasedSampler(new AlwaysOnSampler())
        };
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToskaMesh.Common.Extensions;
using ToskaMesh.Common.Health;
using ToskaMesh.Protocols;
using ToskaMesh.Security;
using ToskaMesh.Telemetry;

namespace ToskaMesh.Runtime;

/// <summary>
/// Entry points for configuring a Toska Mesh-aware service.
/// </summary>
public static class MeshRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Register Toska Mesh infrastructure, telemetry, and auto-registration for a stateless HTTP service.
    /// </summary>
    public static IServiceCollection AddMeshService(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MeshServiceOptions>? configureOptions = null)
    {
        var options = MeshServiceOptions.FromConfiguration(configuration);
        configureOptions?.Invoke(options);
        options.EnsureDefaults();

        services.AddSingleton(options);

        services.AddMeshInfrastructure(
            configuration,
            opt =>
            {
                opt.EnableMassTransit = false;
                opt.EnableRedisCache = false;
                opt.ServiceRegistryProvider = options.ServiceRegistryProvider;
                opt.EnableHealthChecks = false; // add once below
            });

        if (options.EnableTelemetry)
        {
            services.AddMeshTelemetry(options.ServiceName);
        }

        if (options.EnableAuth)
        {
            services.AddMeshAuthorizationPolicies();
        }

        services.AddMeshHealthChecks();
        services.AddHostedService<MeshAutoRegistrar>();

        return services;
    }

    /// <summary>
    /// Map standard mesh endpoints (health + Prometheus scraping).
    /// </summary>
    public static WebApplication UseMeshDefaults(this WebApplication app)
    {
        app.UseMeshHealthChecks();
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }
}

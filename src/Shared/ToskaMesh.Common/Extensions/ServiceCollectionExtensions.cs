using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ToskaMesh.Common.Caching;
using ToskaMesh.Common.Health;
using ToskaMesh.Common.Messaging;
using ToskaMesh.Common.ServiceDiscovery;

namespace ToskaMesh.Common.Extensions;

/// <summary>
/// Extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add common mesh utilities to the service collection
    /// </summary>
    public static IServiceCollection AddMeshCommon(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Add structured logging configuration
    /// </summary>
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
        });

        return services;
    }

    /// <summary>
    /// Registers the standard Toska Mesh infrastructure components (common utilities, Consul, MassTransit, Redis, health checks).
    /// </summary>
    public static IServiceCollection AddMeshInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MeshInfrastructureOptions>? configureOptions = null,
        Action<IBusRegistrationConfigurator>? configureMassTransit = null,
        Action<IHealthChecksBuilder>? configureHealthChecks = null)
    {
        var options = new MeshInfrastructureOptions();
        configureOptions?.Invoke(options);

        services.AddMeshCommon();

        if (options.EnableConsulServiceRegistry)
        {
            if (options.ServiceRegistryProvider == ServiceRegistryProvider.Grpc)
            {
                services.AddGrpcServiceRegistry(configuration);
            }
            else
            {
                services.AddConsulServiceRegistry(configuration);
            }
        }

        if (options.EnableMassTransit)
        {
            services.AddMeshMassTransit(configuration, configureMassTransit);
        }

        if (options.EnableRedisCache)
        {
            services.AddRedisCache(configuration);
        }

        options.ConfigureDatabase?.Invoke(services, configuration);
        options.ConfigureAdditionalServices?.Invoke(services, configuration);

        if (options.EnableHealthChecks)
        {
            var healthChecks = services.AddMeshHealthChecks();
            configureHealthChecks?.Invoke(healthChecks);
        }

        return services;
    }
}

/// <summary>
/// Options for configuring Mesh infrastructure registration.
/// </summary>
public class MeshInfrastructureOptions
{
    public bool EnableMassTransit { get; set; }
    public bool EnableRedisCache { get; set; }
    public bool EnableConsulServiceRegistry { get; set; } = true;
    public ServiceRegistryProvider ServiceRegistryProvider { get; set; } = ServiceRegistryProvider.Consul;
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Optional callback to register database services (e.g., DbContexts).
    /// </summary>
    public Action<IServiceCollection, IConfiguration>? ConfigureDatabase { get; set; }

    /// <summary>
    /// Optional callback for registering additional custom infrastructure components.
    /// </summary>
    public Action<IServiceCollection, IConfiguration>? ConfigureAdditionalServices { get; set; }
}

public enum ServiceRegistryProvider
{
    Consul,
    Grpc
}

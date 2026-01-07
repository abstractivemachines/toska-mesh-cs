using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ToskaMesh.Runtime;

/// <summary>
/// Shared bootstrap logic for all mesh service hosting styles (lambda, base-class, stateful).
/// </summary>
public static class MeshServiceBootstrap
{
    /// <summary>
    /// Configure common mesh services: options singleton, infrastructure, telemetry, auth, health, and auto-registration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Host environment for determining noop registry allowance.</param>
    /// <param name="options">Pre-configured mesh service options (EnsureDefaults should already be called).</param>
    public static void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        MeshServiceOptions options)
    {
        // Use TryAdd so callers can register options earlier if needed (e.g., for user service injection)
        services.TryAddSingleton(options);

        services.AddMeshService(configuration, opt =>
        {
            opt.ServiceName = options.ServiceName;
            opt.ServiceId = options.ServiceId;
            opt.Address = options.Address;
            opt.Port = options.Port;
            opt.HealthEndpoint = options.HealthEndpoint;
            opt.HealthInterval = options.HealthInterval;
            opt.HealthTimeout = options.HealthTimeout;
            opt.UnhealthyThreshold = options.UnhealthyThreshold;
            opt.EnableTelemetry = options.EnableTelemetry;
            opt.EnableAuth = options.EnableAuth;
            opt.RegisterAutomatically = options.RegisterAutomatically;
            opt.ServiceRegistryProvider = options.ServiceRegistryProvider;
            opt.Metadata = new Dictionary<string, string>(options.Metadata, StringComparer.OrdinalIgnoreCase);
        });

        services.TryAddMeshServiceRegistryStub(options, environment);
    }

    /// <summary>
    /// Create and configure mesh service options from configuration and an optional configurator.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="configureOptions">Optional callback to further configure options.</param>
    /// <returns>Configured options with defaults applied.</returns>
    public static MeshServiceOptions CreateOptions(
        IConfiguration configuration,
        Action<MeshServiceOptions>? configureOptions = null)
    {
        var options = MeshServiceOptions.FromConfiguration(configuration);
        configureOptions?.Invoke(options);
        options.EnsureDefaults();
        return options;
    }

    /// <summary>
    /// Create options and configure services in one call.
    /// </summary>
    public static MeshServiceOptions ConfigureAll(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<MeshServiceOptions>? configureOptions = null)
    {
        var options = CreateOptions(configuration, configureOptions);
        ConfigureServices(services, configuration, environment, options);
        return options;
    }
}

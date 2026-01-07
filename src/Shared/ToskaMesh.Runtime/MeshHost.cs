using Microsoft.Extensions.DependencyInjection;

namespace ToskaMesh.Runtime;

/// <summary>
/// Unified entry point for building mesh services using a fluent API.
/// </summary>
public static class MeshHost
{
    /// <summary>
    /// Create a builder for a stateless (lambda-style) mesh service.
    /// </summary>
    public static MeshLambdaBuilder Lambda() => new();

    /// <summary>
    /// Create a builder for a stateless mesh service with a specific app configuration.
    /// Convenience overload for simple cases.
    /// </summary>
    public static MeshLambdaBuilder Lambda(Action<MeshServiceApp> configureApp) =>
        new MeshLambdaBuilder().WithEndpoints(configureApp);
}

/// <summary>
/// Fluent builder for stateless mesh services.
/// </summary>
public sealed class MeshLambdaBuilder
{
    private Action<MeshServiceApp>? _configureApp;
    private Action<MeshServiceOptions>? _configureOptions;
    private Action<IServiceCollection>? _configureServices;

    /// <summary>
    /// Configure endpoint mappings for the service.
    /// </summary>
    public MeshLambdaBuilder WithEndpoints(Action<MeshServiceApp> configureApp)
    {
        _configureApp = configureApp;
        return this;
    }

    /// <summary>
    /// Configure mesh service options.
    /// </summary>
    public MeshLambdaBuilder WithOptions(Action<MeshServiceOptions> configureOptions)
    {
        _configureOptions = configureOptions;
        return this;
    }

    /// <summary>
    /// Configure additional services.
    /// </summary>
    public MeshLambdaBuilder WithServices(Action<IServiceCollection> configureServices)
    {
        _configureServices = configureServices;
        return this;
    }

    /// <summary>
    /// Run the service until shutdown.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        EnsureAppConfigured();
        return MeshLambdaService.RunAsync(_configureApp!, _configureOptions, _configureServices, cancellationToken);
    }

    /// <summary>
    /// Start the service and return a handle for testing/embedding.
    /// </summary>
    public Task<MeshLambdaServiceHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        EnsureAppConfigured();
        return MeshLambdaService.StartAsync(_configureApp!, _configureOptions, _configureServices, cancellationToken);
    }

    private void EnsureAppConfigured()
    {
        if (_configureApp is null)
        {
            throw new InvalidOperationException("Endpoint configuration is required. Call WithEndpoints() before running.");
        }
    }
}

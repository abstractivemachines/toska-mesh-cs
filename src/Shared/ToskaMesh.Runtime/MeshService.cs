using Microsoft.Extensions.DependencyInjection;

namespace ToskaMesh.Runtime;

/// <summary>
/// Base-class convenience wrapper for hosting a mesh service using overrides instead of lambdas.
/// </summary>
public abstract class MeshService
{
    /// <summary>
    /// Override to map endpoints and middleware.
    /// </summary>
    /// <param name="app">Lightweight mesh app wrapper.</param>
    public abstract void ConfigureApp(MeshServiceApp app);

    /// <summary>
    /// Override to tweak mesh options before hosting.
    /// </summary>
    /// <param name="options">Mesh runtime options bound from configuration.</param>
    public virtual void ConfigureOptions(MeshServiceOptions options)
    {
    }

    /// <summary>
    /// Override to register additional services before hosting.
    /// </summary>
    /// <param name="services">Service collection used by the mesh host.</param>
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Run the service until shutdown.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default) =>
        MeshServiceHost.RunAsync(ConfigureApp, ConfigureOptions, ConfigureServices, cancellationToken);

    /// <summary>
    /// Start the service and return a handle to control the host (useful for tests).
    /// </summary>
    public Task<MeshServiceHostHandle> StartAsync(CancellationToken cancellationToken = default) =>
        MeshServiceHost.StartAsync(ConfigureApp, ConfigureOptions, ConfigureServices, cancellationToken);

    /// <summary>
    /// Run a service that uses a parameterless constructor.
    /// </summary>
    public static Task RunAsync<TService>(CancellationToken cancellationToken = default)
        where TService : MeshService, new() =>
        new TService().RunAsync(cancellationToken);

    /// <summary>
    /// Start a service that uses a parameterless constructor and return a host handle.
    /// </summary>
    public static Task<MeshServiceHostHandle> StartAsync<TService>(CancellationToken cancellationToken = default)
        where TService : MeshService, new() =>
        new TService().StartAsync(cancellationToken);
}

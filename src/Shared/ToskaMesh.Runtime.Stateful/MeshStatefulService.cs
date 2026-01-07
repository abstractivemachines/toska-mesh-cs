using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Runtime;

namespace ToskaMesh.Runtime.Stateful;

/// <summary>
/// Base-class convenience wrapper for hosting a stateful mesh service using overrides instead of lambdas.
/// Mirrors the <see cref="MeshService"/> pattern for stateless services.
/// </summary>
public abstract class MeshStatefulService
{
    /// <summary>
    /// Override to configure stateful host options (Orleans cluster, storage, etc.).
    /// </summary>
    /// <param name="options">Stateful runtime options.</param>
    public virtual void ConfigureStateful(StatefulHostOptions options)
    {
    }

    /// <summary>
    /// Override to tweak mesh service options before hosting.
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
    /// Run the stateful service until shutdown.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default) =>
        MeshStatefulLambdaService.RunAsync(ConfigureStateful, ConfigureOptions, ConfigureServices, cancellationToken);

    /// <summary>
    /// Start the stateful service and return a handle to control the host (useful for tests).
    /// </summary>
    public IHost Start() =>
        MeshStatefulLambdaService.Start(ConfigureStateful, ConfigureOptions, ConfigureServices);

    /// <summary>
    /// Run a stateful service that uses a parameterless constructor.
    /// </summary>
    public static Task RunAsync<TService>(CancellationToken cancellationToken = default)
        where TService : MeshStatefulService, new() =>
        new TService().RunAsync(cancellationToken);

    /// <summary>
    /// Start a stateful service that uses a parameterless constructor and return a host handle.
    /// </summary>
    public static IHost Start<TService>()
        where TService : MeshStatefulService, new() =>
        new TService().Start();
}

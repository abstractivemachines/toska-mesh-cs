using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Runtime;

namespace ToskaMesh.Runtime.Stateful;

/// <summary>
/// Unified entry point for building stateful mesh services using a fluent API.
/// Mirrors <see cref="MeshHost"/> for stateless services.
/// </summary>
/// <example>
/// <code>
/// await MeshStatefulHost.Create()
///     .WithProvider(opts => opts.Orleans.ClusterId = "my-cluster")
///     .WithOptions(opts => opts.ServiceName = "my-silo")
///     .RunAsync();
/// </code>
/// </example>
public static class MeshStatefulHost
{
    /// <summary>
    /// Create a builder for a stateful mesh service.
    /// </summary>
    public static MeshStatefulBuilder Create() => new();

    /// <summary>
    /// Create a builder with provider configuration.
    /// Convenience overload for simple cases.
    /// </summary>
    public static MeshStatefulBuilder Create(Action<StatefulHostOptions> configureStateful) =>
        new MeshStatefulBuilder().WithProvider(configureStateful);
}

/// <summary>
/// Fluent builder for stateful mesh services.
/// </summary>
public sealed class MeshStatefulBuilder
{
    private Action<StatefulHostOptions>? _configureStateful;
    private Action<MeshServiceOptions>? _configureOptions;
    private Action<IServiceCollection>? _configureServices;

    /// <summary>
    /// Configure stateful provider options (Orleans cluster, storage, etc.).
    /// </summary>
    public MeshStatefulBuilder WithProvider(Action<StatefulHostOptions> configureStateful)
    {
        _configureStateful = configureStateful;
        return this;
    }

    /// <summary>
    /// Configure mesh service options.
    /// </summary>
    public MeshStatefulBuilder WithOptions(Action<MeshServiceOptions> configureOptions)
    {
        _configureOptions = configureOptions;
        return this;
    }

    /// <summary>
    /// Configure additional services.
    /// </summary>
    public MeshStatefulBuilder WithServices(Action<IServiceCollection> configureServices)
    {
        _configureServices = configureServices;
        return this;
    }

    /// <summary>
    /// Run the stateful service until shutdown.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default) =>
        MeshStatefulLambdaService.RunAsync(_configureStateful, _configureOptions, _configureServices, cancellationToken);

    /// <summary>
    /// Start the stateful service and return a host handle.
    /// </summary>
    public IHost Start() =>
        MeshStatefulLambdaService.Start(_configureStateful, _configureOptions, _configureServices);
}

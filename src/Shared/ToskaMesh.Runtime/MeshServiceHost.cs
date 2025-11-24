using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Protocols;

namespace ToskaMesh.Runtime;

/// <summary>
/// High-level runtime wrapper for hosting mesh-aware services without exposing ASP.NET Core/Orleans details.
/// </summary>
public static class MeshServiceHost
{
    /// <summary>
    /// Run a stateless mesh service until shutdown.
    /// </summary>
    public static async Task RunAsync(
        Action<MeshServiceApp> configureApp,
        Action<MeshServiceOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        await using var handle = await StartInternalAsync(configureApp, configureOptions, configureServices, useTestServer: false, cancellationToken);
        await handle.App.RunAsync(cancellationToken);
    }

    /// <summary>
    /// Start a stateless mesh service in-memory for testing and return a handle with client/services access.
    /// </summary>
    public static Task<MeshServiceHostHandle> StartInMemoryAsync(
        Action<MeshServiceApp> configureApp,
        Action<MeshServiceOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        return StartInternalAsync(configureApp, configureOptions, configureServices, useTestServer: true, cancellationToken);
    }

    private static async Task<MeshServiceHostHandle> StartInternalAsync(
        Action<MeshServiceApp> configureApp,
        Action<MeshServiceOptions>? configureOptions,
        Action<IServiceCollection>? configureServices,
        bool useTestServer,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        if (useTestServer)
        {
            builder.WebHost.UseTestServer();
        }

        var options = MeshServiceOptions.FromConfiguration(builder.Configuration);
        configureOptions?.Invoke(options);
        options.EnsureDefaults();

        builder.Services.AddSingleton(options);
        configureServices?.Invoke(builder.Services);

        // If the caller didn't register a service registry, use a no-op stub so tests can run without infrastructure.
        builder.Services.TryAddMeshServiceRegistryStub();

        builder.Services.AddMeshService(builder.Configuration, opt =>
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

        var app = builder.Build();
        var meshApp = new MeshServiceApp(app);
        configureApp(meshApp);
        app.UseMeshDefaults();

        if (useTestServer)
        {
            await app.StartAsync(cancellationToken);
            var client = app.GetTestClient();
            return new MeshServiceHostHandle(app, client);
        }

        // When not using test server, start and return handle without a client.
        await app.StartAsync(cancellationToken);
        return new MeshServiceHostHandle(app, null);
    }
}

/// <summary>
/// Thin wrapper exposing mapping helpers without leaking WebApplication to callers.
/// </summary>
public sealed class MeshServiceApp
{
    private readonly WebApplication _app;

    internal MeshServiceApp(WebApplication app)
    {
        _app = app;
    }

    public void MapGet(string pattern, Delegate handler) => _app.MapGet(pattern, handler);
    public void MapPost(string pattern, Delegate handler) => _app.MapPost(pattern, handler);
    public void MapPut(string pattern, Delegate handler) => _app.MapPut(pattern, handler);
    public void MapDelete(string pattern, Delegate handler) => _app.MapDelete(pattern, handler);

    public void Map(string pattern, Delegate handler, string method = "GET")
    {
        _app.MapMethods(pattern, new[] { method }, handler);
    }
}

/// <summary>
/// Handle for an in-memory host (primarily for testing).
/// </summary>
public sealed class MeshServiceHostHandle : IAsyncDisposable
{
    internal MeshServiceHostHandle(WebApplication app, HttpClient? client)
    {
        App = app;
        Client = client;
    }

    internal WebApplication App { get; }
    public HttpClient? Client { get; }
    public IServiceProvider Services => App.Services;

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
    }
}

internal static class MeshServiceHostServiceCollectionExtensions
{
    public static void TryAddMeshServiceRegistryStub(this IServiceCollection services)
    {
        var hasRegistry = services.Any(d => d.ServiceType == typeof(IServiceRegistry));
        if (!hasRegistry)
        {
            services.AddSingleton<IServiceRegistry, NoopServiceRegistry>();
        }
    }

    private sealed class NoopServiceRegistry : IServiceRegistry
    {
        public Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceRegistrationResult(true, registration.ServiceId));

        public Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ServiceInstance>>(Array.Empty<ServiceInstance>());

        public Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceInstance?>(null);

        public Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}

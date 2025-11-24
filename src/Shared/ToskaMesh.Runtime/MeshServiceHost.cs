using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToskaMesh.Runtime.Orleans;
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
        await using var handle = await StartAsync(configureApp, configureOptions, configureServices, cancellationToken);
        await handle.App.WaitForShutdownAsync(cancellationToken);
    }

    /// <summary>
    /// Start a stateless mesh service and return a handle with client/services access (useful for tests and embedding).
    /// </summary>
    public static async Task<MeshServiceHostHandle> StartAsync(
        Action<MeshServiceApp> configureApp,
        Action<MeshServiceOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        var options = MeshServiceOptions.FromConfiguration(builder.Configuration);
        configureOptions?.Invoke(options);
        options.EnsureDefaults();

        // Allow dynamic port selection (e.g., tests) by setting Port=0.
        builder.WebHost.UseUrls($"http://{options.Address}:{options.Port}");

        builder.Services.AddSingleton(options);
        configureServices?.Invoke(builder.Services);

        // If the caller didn't register a service registry, use a no-op stub only if explicitly allowed.
        builder.Services.TryAddMeshServiceRegistryStub(options, builder.Environment);

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

        await app.StartAsync(cancellationToken);

        // Use the bound address (handles dynamic port if Port=0).
        var baseAddress = app.Urls.FirstOrDefault() ?? $"http://{options.Address}:{options.Port}";
        var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

        return new MeshServiceHostHandle(app, client);
    }

    /// <summary>
    /// Run a stateful (Orleans-backed) mesh service without exposing silo configuration.
    /// </summary>
    public static async Task RunStatefulAsync(
        Action<MeshStatefulOptions>? configureSilo = null,
        Action<MeshServiceOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        using var host = CreateStatefulHost(configureSilo, configureOptions, configureServices);
        await host.StartAsync(cancellationToken);
        await host.WaitForShutdownAsync(cancellationToken);
    }

    /// <summary>
    /// Start a stateful (Orleans-backed) mesh service and return the host (useful for tests/embedding).
    /// </summary>
    public static IHost StartStateful(
        Action<MeshStatefulOptions>? configureSilo = null,
        Action<MeshServiceOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = CreateStatefulHost(configureSilo, configureOptions, configureServices);
        host.Start();
        return host;
    }

    private static IHost CreateStatefulHost(
        Action<MeshStatefulOptions>? configureSilo,
        Action<MeshServiceOptions>? configureOptions,
        Action<IServiceCollection>? configureServices)
    {
        var builder = Host.CreateDefaultBuilder();

        builder.UseMeshSilo("mesh-stateful-service", configureSilo);

        builder.ConfigureServices((context, services) =>
        {
            services.AddMeshService(context.Configuration, configureOptions);
            configureServices?.Invoke(services);
        });

        return builder.Build();
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

    /// <summary>
    /// Register a custom middleware in the request pipeline without exposing the underlying WebApplication.
    /// </summary>
    public void Use(Func<HttpContext, Func<Task>, Task> middleware)
    {
        _app.Use(async (context, next) => await middleware(context, next));
    }
}

/// <summary>
/// Handle for a running host (supports HTTP client + DI access).
/// </summary>
public sealed class MeshServiceHostHandle : IAsyncDisposable
{
    internal MeshServiceHostHandle(WebApplication app, HttpClient client)
    {
        App = app;
        Client = client;
    }

    internal WebApplication App { get; }
    public HttpClient Client { get; }
    public IServiceProvider Services => App.Services;

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        Client.Dispose();
    }
}

internal static class MeshServiceHostServiceCollectionExtensions
{
    public static void TryAddMeshServiceRegistryStub(this IServiceCollection services, MeshServiceOptions options, IHostEnvironment env)
    {
        var hasRegistry = services.Any(d => d.ServiceType == typeof(IServiceRegistry));
        if (!hasRegistry)
        {
            services.AddSingleton<IServiceRegistry, NoopServiceRegistry>();
            var allowNoop = options.AllowNoopServiceRegistry || string.Equals(env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
            if (!allowNoop)
            {
                throw new InvalidOperationException("No IServiceRegistry registered and AllowNoopServiceRegistry is false. Register a registry or explicitly allow noop for tests/dev.");
            }
            Console.WriteLine($"[MeshServiceHost] WARNING: No IServiceRegistry registered; using noop registry for service '{options.ServiceName}' in {env.EnvironmentName}.");
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

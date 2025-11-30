using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using TodoMeshService.Contracts;
using ToskaMesh.Runtime;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var consulAddress = configuration.GetValue<string>("Consul:Address") ?? "http://consul:8500";
var clusterId = configuration.GetValue<string>("Orleans:ClusterId") ?? "mesh-stateful";
var serviceId = configuration.GetValue<string>("Orleans:ServiceId") ?? "todo-mesh-service";

try
{
    await MeshServiceHost.RunAsync(
        app =>
        {
            app.MapGet("/todos/{id}", async (string id, IGrainFactory grains) =>
            {
                var grain = grains.GetGrain<ITodoGrain>(id);
                var todo = await grain.GetAsync();
                return todo is null ? Results.NotFound() : Results.Ok(todo);
            });

            app.MapPost("/todos/{id}", async (string id, TodoCommand cmd, IGrainFactory grains) =>
            {
                var grain = grains.GetGrain<ITodoGrain>(id);
                var todo = await grain.UpsertAsync(cmd.Title, cmd.Completed);
                return Results.Created($"/todos/{todo.Id}", todo);
            });

            app.MapPut("/todos/{id}", async (string id, TodoCommand cmd, IGrainFactory grains) =>
            {
                var grain = grains.GetGrain<ITodoGrain>(id);
                var todo = await grain.UpsertAsync(cmd.Title, cmd.Completed);
                return Results.Ok(todo);
            });

            app.MapDelete("/todos/{id}", async (string id, IGrainFactory grains) =>
            {
                var deleted = await grains.GetGrain<ITodoGrain>(id).DeleteAsync();
                return deleted ? Results.NoContent() : Results.NotFound();
            });
        },
        options =>
        {
            options.ServiceName = "todo-mesh-api";
            options.Routing.HealthCheckEndpoint = "/health";
        },
        services =>
        {
            Console.WriteLine($"[todo-mesh-api] Configuring Orleans client for cluster '{clusterId}' service '{serviceId}' via Consul {consulAddress}...");

            services.AddOrleansClient(builder =>
            {
                builder.Configure<ClusterOptions>(o =>
                {
                    o.ClusterId = clusterId;
                    o.ServiceId = serviceId;
                });

                builder.UseConsulClientClustering(opt =>
                {
                    opt.KvRootFolder = $"orleans/{clusterId}";
                    opt.ConfigureConsulClient(new Uri(consulAddress));
                });

                builder.UseConnectionRetryFilter(async (exception, ct) =>
                {
                    Console.WriteLine(
                        $"[todo-mesh-api] Orleans client failed to connect ({exception.GetType().Name}: {exception.Message}). Retrying in 5s...");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }

                    return true;
                });
            });

            // Orleans registers the client itself as an IHostedService, which blocks the web host startup
            // until the client connects. Remove that hosted service so we can bring up Kestrel and surface
            // connection errors via logs/health checks instead of crash-looping.
            var orleansHosted = services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(IHostedService) &&
                sd.ImplementationInstance is null &&
                sd.ImplementationType is null &&
                sd.ImplementationFactory is not null);
            if (orleansHosted is not null)
            {
                services.Remove(orleansHosted);
            }

            services.AddSingleton<OrleansClientConnectionState>();
            services.AddHostedService<OrleansClientConnector>();
            services.AddHealthChecks().AddCheck<OrleansClientHealthCheck>("orleans-client", tags: new[] { "ready" });
            services.AddSingleton<IGrainFactory>(sp => sp.GetRequiredService<IClusterClient>());
        });
}
catch (Exception ex)
{
    Console.WriteLine($"[todo-mesh-api] Fatal: {ex}");
    throw;
}

public record TodoCommand(string Title, bool Completed);

public sealed class OrleansClientConnectionState
{
    public bool Connected { get; private set; }

    public void MarkConnected() => Connected = true;
}

public sealed class OrleansClientConnector : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IClusterClient clusterClient;
    private readonly IHostedService clusterClientHostedService;
    private readonly OrleansClientConnectionState state;
    private readonly ILogger<OrleansClientConnector> logger;

    public OrleansClientConnector(
        IClusterClient clusterClient,
        OrleansClientConnectionState state,
        ILogger<OrleansClientConnector> logger)
    {
        this.clusterClient = clusterClient;
        clusterClientHostedService = clusterClient as IHostedService
            ?? throw new InvalidOperationException("Orleans client must implement IHostedService.");
        this.state = state;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !state.Connected)
        {
            try
            {
                await clusterClientHostedService.StartAsync(stoppingToken);
                state.MarkConnected();
                logger.LogInformation("[todo-mesh-api] Orleans client connected to cluster.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "[todo-mesh-api] Orleans client connection failed; retrying in {DelaySeconds}s",
                    RetryDelay.TotalSeconds);

                try
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (state.Connected)
        {
            try
            {
                await clusterClientHostedService.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[todo-mesh-api] Failed to stop Orleans client cleanly.");
            }
        }

        await base.StopAsync(cancellationToken);
    }
}

public sealed class OrleansClientHealthCheck : IHealthCheck
{
    private readonly OrleansClientConnectionState state;

    public OrleansClientHealthCheck(OrleansClientConnectionState state)
    {
        this.state = state;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            state.Connected
                ? HealthCheckResult.Healthy("Orleans client connected")
                : HealthCheckResult.Unhealthy("Orleans client not connected"));
    }
}

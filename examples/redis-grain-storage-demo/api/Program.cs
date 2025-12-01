using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using RedisGrainDemo.Contracts;
using ToskaMesh.Runtime;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var clusterId = configuration.GetValue<string>("Orleans:ClusterId") ?? "redis-grain-demo";
var serviceId = configuration.GetValue<string>("Orleans:ServiceId") ?? "redis-grain-silo";
var gatewayPort = configuration.GetValue<int?>("Orleans:GatewayPort") ?? 30000;
var siloPort = configuration.GetValue<int?>("Orleans:SiloPort") ?? 11111;

await MeshServiceHost.RunAsync(
    app =>
    {
        app.MapGet("/counter", async (IGrainFactory grains) =>
        {
            var grain = grains.GetGrain<ICounterGrain>("counter");
            var value = await grain.GetAsync();
            return Results.Ok(new { value });
        });

        app.MapPost("/counter/increment", async (IncrementRequest request, IGrainFactory grains) =>
        {
            var grain = grains.GetGrain<ICounterGrain>("counter");
            var value = await grain.IncrementAsync(request.Delta);
            return Results.Ok(new { value });
        });

        app.MapPost("/counter/reset", async (IGrainFactory grains) =>
        {
            var grain = grains.GetGrain<ICounterGrain>("counter");
            await grain.ResetAsync();
            return Results.NoContent();
        });

        app.MapGet("/health", () => Results.Ok("ok"));
    },
    options =>
    {
        options.ServiceName = "redis-grain-api";
        options.Port = 8080;
        options.Routing.HealthCheckEndpoint = "/health";
        options.RegisterAutomatically = true;
        options.AllowNoopServiceRegistry = false;
        options.ServiceRegistryProvider = ToskaMesh.Common.Extensions.ServiceRegistryProvider.Consul;
    },
    services =>
    {
        services.AddOrleansClient(builder =>
        {
            builder.Configure<ClusterOptions>(o =>
            {
                o.ClusterId = clusterId;
                o.ServiceId = serviceId;
            });

            builder.UseLocalhostClustering();

            builder.UseConnectionRetryFilter(async (ex, ct) =>
            {
                Console.WriteLine($"[redis-grain-api] Orleans client connection failed ({ex.Message}); retrying in 3s.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                return true;
            });
        });

        // Orleans registers the client as an IHostedService that blocks startup; remove it and manage via connector.
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
        services.AddSingleton<IGrainFactory>(sp => sp.GetRequiredService<IClusterClient>());
    });

public record IncrementRequest(int Delta);

public sealed class OrleansClientConnectionState
{
    public bool Connected { get; private set; }

    public void MarkConnected() => Connected = true;
}

public sealed class OrleansClientConnector : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

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
                logger.LogInformation("[redis-grain-api] Orleans client connected to cluster.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "[redis-grain-api] Orleans client connection failed; retrying in {DelaySeconds}s",
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
                logger.LogWarning(ex, "[redis-grain-api] Failed to stop Orleans client cleanly.");
            }
        }

        await base.StopAsync(cancellationToken);
    }
}

using Microsoft.Extensions.Configuration;
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
        });

        services.AddSingleton<IGrainFactory>(sp => sp.GetRequiredService<IClusterClient>());
    });

public record TodoCommand(string Title, bool Completed);

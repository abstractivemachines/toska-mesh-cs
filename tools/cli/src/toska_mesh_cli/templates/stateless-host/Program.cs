using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;

// Simple sample service that uses the ToskaMesh.Runtime NuGet package.
// Shows the minimal MeshServiceHost surface with DI and a few routes.
var todoStore = new TodoStore();
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await MeshServiceHost.RunAsync(
    app =>
    {
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.Append("x-mesh-sample", "hello-mesh-service");
            await next();
        });

        app.MapGet("/hello", () => Results.Ok(new { message = "Hello from ToskaMesh" }));

        app.MapGet("/todos", () => Results.Ok(todoStore.GetAll()));

        app.MapGet("/todos/all", () => Results.Ok(todoStore.GetAll()));

        app.MapGet("/todos/{id}", (string id) =>
            todoStore.TryGet(id, out var todo)
                ? Results.Ok(todo)
                : Results.NotFound());

        app.MapPost("/todos", (TodoItem todo) =>
        {
            var created = todoStore.Add(todo);
            return Results.Created($"/todos/{created.Id}", created);
        });
    },
    options =>
    {
        // Keep routing metadata explicit; other service details come from configuration.
        options.Routing.Strategy = LoadBalancingStrategy.RoundRobin;
        options.Routing.HealthCheckEndpoint = "/health";
        options.Routing.Weight = 1;
        options.AllowNoopServiceRegistry = false;
    },
    services =>
    {
        services.AddGrpcServiceRegistry(configuration);
    });

internal sealed record TodoItem(string? Id, string Title, bool Completed);

internal sealed class TodoStore
{
    private readonly ConcurrentDictionary<string, TodoItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public TodoStore()
    {
        Add(new TodoItem(null, "wire up ToskaMesh.Runtime", true));
        Add(new TodoItem(null, "deploy alongside discovery/gateway", false));
    }

    public IReadOnlyCollection<TodoItem> GetAll() => _items.Values.ToArray();

    public bool TryGet(string id, out TodoItem? item) => _items.TryGetValue(id, out item);

    public TodoItem Add(TodoItem item)
    {
        var id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
        var stored = item with { Id = id };
        _items[id] = stored;
        return stored;
    }
}

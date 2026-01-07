using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Common.ServiceDiscovery;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;

// Example stateless mesh service using the lambda hosting style.
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await MeshLambdaService.RunAsync(
    app =>
    {
        app.MapGet("/health", () => Results.Ok("ok"));

        // GET /add?a=1&b=2
        app.MapGet("/add", (double a, double b) =>
            Results.Ok(new { a, b, sum = a + b }));

        // POST /add  { "a": 1, "b": 2 }
        app.MapPost("/add", (AddRequest request) =>
            Results.Ok(new { request.A, request.B, sum = request.A + request.B }));
    },
    options =>
    {
        options.ServiceName ??= "adder-mesh-service";
        options.Address ??= "0.0.0.0";
        options.Port = options.Port == 0 ? 8083 : options.Port;
        options.Routing.HealthCheckEndpoint ??= "/health";
        if (options.Routing.Strategy == default)
        {
            options.Routing.Strategy = LoadBalancingStrategy.RoundRobin;
        }
    },
    services =>
    {
        var provider = configuration.GetValue<string>("Mesh:Service:ServiceRegistryProvider");
        if (string.Equals(provider, "consul", StringComparison.OrdinalIgnoreCase))
        {
            services.AddConsulServiceRegistry(configuration);
        }
        else
        {
            services.AddGrpcServiceRegistry(configuration);
        }
    });

public sealed record AddRequest(double A, double B);

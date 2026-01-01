using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TodoMeshService.Grains;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Stateful;

// Orleans silo hosting todo grains, with Redis-backed key/value store.
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var healthPort = configuration.GetValue<int?>("Mesh:Service:Port") ?? 8085;
await using var healthApp = BuildHealthApp(healthPort);
await healthApp.StartAsync();

try
{
    await StatefulMeshHost.RunAsync(
    configureStateful: opts =>
    {
        opts.ServiceName = "todo-mesh-silo";
        opts.Orleans.ClusterId = "mesh-stateful";
        opts.Orleans.ClusterProvider = StatefulClusterProvider.Consul;
        opts.Orleans.ConsulAddress = configuration.GetValue<string>("Consul:Address") ?? "http://consul:8500";
        opts.Orleans.AdvertisedIPAddress = configuration.GetValue<string>("Orleans:AdvertisedIPAddress");
        opts.KeyValue.Enabled = true;
        opts.KeyValue.ConnectionString = configuration.GetValue<string>("Mesh:KeyValue:Redis:ConnectionString")
            ?? "redis:6379";
        opts.KeyValue.Database = configuration.GetValue<int?>("Mesh:KeyValue:Redis:Database");
        opts.KeyValue.KeyPrefix = configuration.GetValue<string>("Mesh:KeyValue:Redis:KeyPrefix");
    },
    configureService: service =>
    {
        service.ServiceName = "todo-mesh-silo";
        service.Routing.HealthCheckEndpoint = "/health";
        service.ServiceRegistryProvider = ToskaMesh.Common.Extensions.ServiceRegistryProvider.Consul;
    },
    configureServices: services =>
    {
        services.AddSingleton<IPEndPoint>(_ => new IPEndPoint(IPAddress.Any, 0));
    });
}
finally
{
    await healthApp.StopAsync();
}

static WebApplication BuildHealthApp(int port)
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    var app = builder.Build();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
    return app;
}

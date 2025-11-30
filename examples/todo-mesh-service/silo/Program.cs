using System.Net;
using Microsoft.Extensions.Configuration;
using TodoMeshService.Grains;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Stateful;
using Microsoft.Extensions.DependencyInjection;

// Orleans silo hosting todo grains, with Redis-backed key/value store.
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await StatefulMeshHost.RunAsync(
    configureStateful: opts =>
    {
        opts.ServiceName = "todo-mesh-silo";
        opts.Orleans.ClusterId = "mesh-stateful";
        opts.Orleans.ClusterProvider = StatefulClusterProvider.Consul;
        opts.Orleans.ConsulAddress = configuration.GetValue<string>("Consul:Address") ?? "http://consul:8500";
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

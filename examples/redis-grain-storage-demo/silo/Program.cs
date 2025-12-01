using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedisGrainDemo.Silo.Grains;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Stateful;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await StatefulMeshHost.RunAsync(
    configureStateful: stateful =>
    {
        stateful.ServiceName = "redis-grain-silo";
        stateful.Orleans.ClusterId = "redis-grain-demo";
        stateful.Orleans.ClusterProvider = StatefulClusterProvider.Local;
        stateful.Orleans.PrimaryPort = 11111;
        stateful.Orleans.ClientPort = 30000;
        stateful.Orleans.RedisStorageConnectionString =
            configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
        stateful.Orleans.RedisStorageDatabase = configuration.GetValue<int?>("Redis:Database");
        stateful.Orleans.RedisStorageKeyPrefix = configuration.GetValue<string>("Redis:KeyPrefix") ?? "redis-grain-demo:grain:";
    },
    configureService: options =>
    {
        options.ServiceName = "redis-grain-silo";
        options.Routing.HealthCheckEndpoint = "/health";
        options.RegisterAutomatically = true;
        options.AllowNoopServiceRegistry = false;
        options.ServiceRegistryProvider = ToskaMesh.Common.Extensions.ServiceRegistryProvider.Consul;
    },
    configureServices: services =>
    {
        // Bind IP dynamically; Local clustering for development.
        services.AddSingleton<IPEndPoint>(_ => new IPEndPoint(IPAddress.Any, 0));
    });

using Microsoft.Extensions.Hosting;
using ToskaMesh.Core.Configuration;
using ToskaMesh.Telemetry;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddMeshTelemetry(context.Configuration, "Core");
    })
    .UseOrleansSilo()
    .Build();

await host.RunAsync();

using Microsoft.Extensions.Hosting;
using ToskaMesh.Core.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .UseOrleansSilo()
    .Build();

await host.RunAsync();

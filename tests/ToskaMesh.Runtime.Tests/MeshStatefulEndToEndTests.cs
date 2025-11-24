using Microsoft.Extensions.DependencyInjection;
using Orleans;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Orleans;
using ToskaMesh.Runtime.Tests.StatefulSample;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class MeshStatefulEndToEndTests
{
    [Fact]
    public async Task RunStatefulAsync_hosts_grain_and_serves_requests()
    {
        using var host = MeshServiceHost.StartStateful(
            configureSilo: silo =>
            {
                silo.ServiceName = "stateful-e2e";
                silo.PrimaryPort = 21112;
                silo.ClientPort = 21001;
                silo.ClusterProvider = StatefulClusterProvider.Local;
            },
            configureOptions: options =>
            {
                options.ServiceName = "stateful-e2e";
                options.RegisterAutomatically = false;
                options.HeartbeatEnabled = false;
                options.AllowNoopServiceRegistry = true;
            },
            configureServices: services =>
            {
                services.AddSingleton<ITestGrain, TestGrain>();
            });

        var client = host.Services.GetRequiredService<IGrainFactory>();
        var grain = client.GetGrain<ITestGrain>("abc");
        var result = await grain.SayHello("world");

        Assert.Equal("hello world", result);

        await host.StopAsync();
    }
}

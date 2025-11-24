using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Orleans;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class MeshStatefulHostTests
{
    [Fact]
    public async Task Stateful_host_starts_and_stops_with_custom_ports()
    {
        using var host = MeshServiceHost.StartStateful(
            configureSilo: silo =>
            {
                silo.ServiceName = "stateful-test";
                silo.SiloPort = 21111;
                silo.GatewayPort = 21000;
                silo.ClusteringMode = "localhost";
            },
            configureOptions: options =>
            {
                options.ServiceName = "stateful-test";
                options.RegisterAutomatically = false;
                options.HeartbeatEnabled = false;
                options.AllowNoopServiceRegistry = true;
            });

        var statefulOptions = host.Services.GetRequiredService<MeshStatefulOptions>();
        Assert.Equal("stateful-test", statefulOptions.ServiceName);
        Assert.Equal(21111, statefulOptions.SiloPort);

        await host.StopAsync();
    }
}

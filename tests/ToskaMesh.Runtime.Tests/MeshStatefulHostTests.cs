using System;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Orleans;
using ToskaMesh.Security;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class MeshStatefulHostTests
{
    [Fact]
    public async Task Stateful_host_starts_and_stops_with_custom_ports()
    {
        Environment.SetEnvironmentVariable("Mesh:ServiceAuth:Secret", new string('s', MeshServiceAuthOptions.MinimumSecretLength));

        using var host = MeshServiceHost.StartStateful(
            configureSilo: silo =>
            {
                silo.ServiceName = "stateful-test";
                silo.PrimaryPort = 21111;
                silo.ClientPort = 21000;
                silo.ClusterProvider = StatefulClusterProvider.Local;
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
        Assert.Equal(21111, statefulOptions.PrimaryPort);

        await host.StopAsync();
    }
}

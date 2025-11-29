using System;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Runtime.Stateful;
using ToskaMesh.Security;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class MeshStatefulHostTests
{
    [Fact]
    public async Task Stateful_host_starts_and_stops_with_custom_ports()
    {
        Environment.SetEnvironmentVariable("Mesh:ServiceAuth:Secret", new string('s', MeshServiceAuthOptions.MinimumSecretLength));

        using var host = StatefulMeshHost.Start(
            configureStateful: options =>
            {
                options.ServiceName = "stateful-test";
                options.ServiceId = "stateful-test";
                options.Orleans.PrimaryPort = 21111;
                options.Orleans.ClientPort = 21000;
                options.Orleans.ClusterProvider = StatefulClusterProvider.Local;
            },
            configureService: options =>
            {
                options.RegisterAutomatically = false;
                options.HeartbeatEnabled = false;
                options.AllowNoopServiceRegistry = true;
            });

        var statefulOptions = host.Services.GetRequiredService<StatefulHostOptions>();
        Assert.Equal("stateful-test", statefulOptions.ServiceName);
        Assert.Equal(21111, statefulOptions.Orleans.PrimaryPort);

        await host.StopAsync();
    }
}

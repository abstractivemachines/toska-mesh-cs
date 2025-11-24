using Microsoft.Extensions.Logging.Abstractions;
using ToskaMesh.Protocols;
using ToskaMesh.Runtime;
using Xunit;

namespace ToskaMesh.Runtime.Tests;

public class MeshHeartbeatServiceTests
{
    [Fact]
    public async Task Heartbeat_updates_registry_when_service_registered()
    {
        var registry = new RecordingRegistry();
        var options = new MeshServiceOptions
        {
            ServiceName = "hb-test",
            HealthInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatEnabled = true
        };
        var state = new MeshRegistrationState { ServiceId = "svc-1" };
        var service = new MeshHeartbeatService(registry, options, state, NullLogger<MeshHeartbeatService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(75));
        await service.StartAsync(cts.Token);
        await Task.Delay(80);
        await service.StopAsync(CancellationToken.None);

        Assert.True(registry.Updates.Count >= 1);
        Assert.All(registry.Updates, id => Assert.Equal("svc-1", id));
    }

    [Fact]
    public async Task Heartbeat_does_nothing_when_no_service_id()
    {
        var registry = new RecordingRegistry();
        var options = new MeshServiceOptions
        {
            ServiceName = "hb-test",
            HealthInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatEnabled = true
        };
        var state = new MeshRegistrationState { ServiceId = null };
        var service = new MeshHeartbeatService(registry, options, state, NullLogger<MeshHeartbeatService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await service.StartAsync(cts.Token);
        await Task.Delay(60);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(registry.Updates);
    }

    [Fact]
    public async Task Heartbeat_retries_on_failure_and_logs_degraded_on_shutdown()
    {
        var registry = new FlakyRegistry(failuresBeforeSuccess: 2);
        var options = new MeshServiceOptions
        {
            ServiceName = "hb-test",
            HealthInterval = TimeSpan.FromMilliseconds(50),
            HeartbeatEnabled = true
        };
        var state = new MeshRegistrationState { ServiceId = "svc-1" };
        var service = new MeshHeartbeatService(registry, options, state, NullLogger<MeshHeartbeatService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        await service.StartAsync(cts.Token);
        await Task.Delay(450);
        await service.StopAsync(CancellationToken.None);

        Assert.True(registry.Attempts >= 2);
        Assert.Contains(("svc-1", HealthStatus.Healthy), registry.Updates);
        Assert.Contains(("svc-1", HealthStatus.Degraded), registry.Updates);
    }

    private sealed class RecordingRegistry : IServiceRegistry
    {
        public List<string> Updates { get; } = new();

        public Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceInstance?>(null);

        public Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ServiceInstance>>(Array.Empty<ServiceInstance>());

        public Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceRegistrationResult(true, registration.ServiceId));

        public Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default)
        {
            Updates.Add(serviceId);
            return Task.FromResult(true);
        }
    }

    private sealed class FlakyRegistry : IServiceRegistry
    {
        private int _failures;

        public FlakyRegistry(int failuresBeforeSuccess)
        {
            _failures = failuresBeforeSuccess;
        }

        public int Attempts { get; private set; }
        public List<(string ServiceId, HealthStatus Status)> Updates { get; } = new();

        public Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default) => Task.FromResult<ServiceInstance?>(null);

        public Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<ServiceInstance>>(Array.Empty<ServiceInstance>());

        public Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(new ServiceRegistrationResult(true, registration.ServiceId));

        public Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (_failures-- > 0)
            {
                throw new InvalidOperationException("simulated failure");
            }
            Updates.Add((serviceId, status));
            return Task.FromResult(true);
        }
    }
}

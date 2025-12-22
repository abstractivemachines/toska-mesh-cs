using FluentAssertions;
using ToskaMesh.Protocols;
using ToskaMesh.Router.Services;
using Xunit;

namespace ToskaMesh.Router.Tests;

public class LoadBalancerServiceTests
{
    [Fact]
    public async Task SelectInstanceAsync_WithNoInstances_ReturnsNull()
    {
        var registry = new InMemoryServiceRegistry();
        var loadBalancer = new LoadBalancerService(registry);

        var result = await loadBalancer.SelectInstanceAsync("nonexistent-service", new LoadBalancingContext());

        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectInstanceAsync_WithSingleInstance_ReturnsThatInstance()
    {
        var registry = new InMemoryServiceRegistry();
        var instance = CreateInstance("svc-1", "my-service", HealthStatus.Healthy);
        registry.AddInstance(instance);

        var loadBalancer = new LoadBalancerService(registry);

        var result = await loadBalancer.SelectInstanceAsync("my-service", new LoadBalancingContext());

        result.Should().NotBeNull();
        result!.ServiceId.Should().Be("svc-1");
    }

    [Fact]
    public async Task SelectInstanceAsync_RoundRobin_DistributesEvenly()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstance("svc-1", "api", HealthStatus.Healthy));
        registry.AddInstance(CreateInstance("svc-2", "api", HealthStatus.Healthy));
        registry.AddInstance(CreateInstance("svc-3", "api", HealthStatus.Healthy));

        var loadBalancer = new LoadBalancerService(registry);
        var context = new LoadBalancingContext();

        var selections = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            var selected = await loadBalancer.SelectInstanceAsync("api", context);
            selections.Add(selected!.ServiceId);
        }

        // Round robin should distribute evenly - each should be selected 3 times
        selections.Count(s => s == "svc-1").Should().Be(3);
        selections.Count(s => s == "svc-2").Should().Be(3);
        selections.Count(s => s == "svc-3").Should().Be(3);
    }

    [Fact]
    public async Task SelectInstanceAsync_PrefersHealthyInstances()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstance("unhealthy-1", "api", HealthStatus.Unhealthy));
        registry.AddInstance(CreateInstance("healthy-1", "api", HealthStatus.Healthy));
        registry.AddInstance(CreateInstance("unhealthy-2", "api", HealthStatus.Unhealthy));

        var loadBalancer = new LoadBalancerService(registry);

        // Should always select the healthy instance
        for (int i = 0; i < 5; i++)
        {
            var result = await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());
            result.Should().NotBeNull();
            result!.ServiceId.Should().Be("healthy-1");
        }
    }

    [Fact]
    public async Task SelectInstanceAsync_FallsBackToNonUnknownWhenNoHealthy()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstance("degraded-1", "api", HealthStatus.Degraded));
        registry.AddInstance(CreateInstance("unknown-1", "api", HealthStatus.Unknown));

        var loadBalancer = new LoadBalancerService(registry);

        var result = await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());

        result.Should().NotBeNull();
        result!.ServiceId.Should().Be("degraded-1");
    }

    [Fact]
    public async Task SelectInstanceAsync_LeastConnections_SelectsInstanceWithFewestConnections()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstanceWithMetadata("svc-1", "api", HealthStatus.Healthy, new() { { "lb_strategy", "LeastConnections" } }));
        registry.AddInstance(CreateInstanceWithMetadata("svc-2", "api", HealthStatus.Healthy, new() { { "lb_strategy", "LeastConnections" } }));

        var loadBalancer = new LoadBalancerService(registry);

        // Select svc-1 first (it has fewer connections)
        var first = await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());
        first.Should().NotBeNull();

        // Select svc-2 next (now svc-1 has a connection)
        var second = await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());
        second.Should().NotBeNull();

        // Should alternate to balance connections
        first!.ServiceId.Should().NotBe(second!.ServiceId);
    }

    [Fact]
    public async Task SelectInstanceAsync_WeightedRoundRobin_RespectsWeights()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstanceWithMetadata("svc-heavy", "api", HealthStatus.Healthy, new() { { "lb_strategy", "WeightedRoundRobin" }, { "weight", "3" } }));
        registry.AddInstance(CreateInstanceWithMetadata("svc-light", "api", HealthStatus.Healthy, new() { { "lb_strategy", "WeightedRoundRobin" }, { "weight", "1" } }));

        var loadBalancer = new LoadBalancerService(registry);

        var selections = new Dictionary<string, int> { { "svc-heavy", 0 }, { "svc-light", 0 } };
        for (int i = 0; i < 8; i++)
        {
            var selected = await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());
            selections[selected!.ServiceId]++;
        }

        // svc-heavy should be selected roughly 3x more than svc-light (6 vs 2 in 8 selections)
        selections["svc-heavy"].Should().BeGreaterThan(selections["svc-light"]);
    }

    [Fact]
    public async Task SelectInstanceAsync_IPHash_ReturnsSameInstanceForSameSession()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstanceWithMetadata("svc-1", "api", HealthStatus.Healthy, new() { { "lb_strategy", "IPHash" } }));
        registry.AddInstance(CreateInstanceWithMetadata("svc-2", "api", HealthStatus.Healthy, new() { { "lb_strategy", "IPHash" } }));
        registry.AddInstance(CreateInstanceWithMetadata("svc-3", "api", HealthStatus.Healthy, new() { { "lb_strategy", "IPHash" } }));

        var loadBalancer = new LoadBalancerService(registry);
        var context = new LoadBalancingContext(SessionId: "user-session-123");

        var first = await loadBalancer.SelectInstanceAsync("api", context);
        var second = await loadBalancer.SelectInstanceAsync("api", context);
        var third = await loadBalancer.SelectInstanceAsync("api", context);

        // Same session should always get the same instance
        first!.ServiceId.Should().Be(second!.ServiceId);
        second.ServiceId.Should().Be(third!.ServiceId);
    }

    [Fact]
    public async Task SelectInstanceAsync_IPHash_DifferentSessionsCanGetDifferentInstances()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstanceWithMetadata("svc-1", "api", HealthStatus.Healthy, new() { { "lb_strategy", "IPHash" } }));
        registry.AddInstance(CreateInstanceWithMetadata("svc-2", "api", HealthStatus.Healthy, new() { { "lb_strategy", "IPHash" } }));

        var loadBalancer = new LoadBalancerService(registry);

        var results = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            var context = new LoadBalancingContext(SessionId: $"session-{i}");
            var selected = await loadBalancer.SelectInstanceAsync("api", context);
            results.Add(selected!.ServiceId);
        }

        // Different sessions should eventually map to different instances
        results.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ReportResultAsync_TracksSuccessfulRequests()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstance("svc-1", "api", HealthStatus.Healthy));

        var loadBalancer = new LoadBalancerService(registry);

        await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());
        await loadBalancer.ReportResultAsync("svc-1", new RequestResult("svc-1", true, TimeSpan.FromMilliseconds(50)));

        var stats = await loadBalancer.GetStatsAsync("api");

        stats.TotalRequests.Should().Be(1);
        stats.SuccessfulRequests.Should().Be(1);
        stats.FailedRequests.Should().Be(0);
    }

    [Fact]
    public async Task ReportResultAsync_TracksFailedRequests()
    {
        var registry = new InMemoryServiceRegistry();
        registry.AddInstance(CreateInstance("svc-1", "api", HealthStatus.Healthy));

        var loadBalancer = new LoadBalancerService(registry);

        await loadBalancer.SelectInstanceAsync("api", new LoadBalancingContext());
        await loadBalancer.ReportResultAsync("svc-1", new RequestResult("svc-1", false, TimeSpan.FromMilliseconds(100), 500, "Internal error"));

        var stats = await loadBalancer.GetStatsAsync("api");

        stats.TotalRequests.Should().Be(1);
        stats.SuccessfulRequests.Should().Be(0);
        stats.FailedRequests.Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsEmptyStatsForUnknownService()
    {
        var registry = new InMemoryServiceRegistry();
        var loadBalancer = new LoadBalancerService(registry);

        var stats = await loadBalancer.GetStatsAsync("unknown-service");

        stats.ServiceName.Should().Be("unknown-service");
        stats.TotalRequests.Should().Be(0);
    }

    private static ServiceInstance CreateInstance(string serviceId, string serviceName, HealthStatus status)
    {
        return new ServiceInstance(
            serviceName,
            serviceId,
            "localhost",
            8080,
            status,
            new Dictionary<string, string>(),
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    private static ServiceInstance CreateInstanceWithMetadata(string serviceId, string serviceName, HealthStatus status, Dictionary<string, string> metadata)
    {
        return new ServiceInstance(
            serviceName,
            serviceId,
            "localhost",
            8080,
            status,
            metadata,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    private sealed class InMemoryServiceRegistry : IServiceRegistry
    {
        private readonly List<ServiceInstance> _instances = new();

        public void AddInstance(ServiceInstance instance) => _instances.Add(instance);

        public Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            var removed = _instances.RemoveAll(i => i.ServiceId == serviceId);
            return Task.FromResult(removed > 0);
        }

        public Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_instances.Select(i => i.ServiceName).Distinct());
        }

        public Task<ServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_instances.FirstOrDefault(i => i.ServiceId == serviceId));
        }

        public Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_instances.Where(i => i.ServiceName == serviceName));
        }

        public Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
        {
            var instance = new ServiceInstance(
                registration.ServiceName,
                registration.ServiceId,
                registration.Address,
                registration.Port,
                HealthStatus.Unknown,
                registration.Metadata,
                DateTime.UtcNow,
                DateTime.UtcNow);
            _instances.Add(instance);
            return Task.FromResult(new ServiceRegistrationResult(true, registration.ServiceId));
        }

        public Task<bool> UpdateHealthStatusAsync(string serviceId, HealthStatus status, CancellationToken cancellationToken = default)
        {
            var index = _instances.FindIndex(i => i.ServiceId == serviceId);
            if (index >= 0)
            {
                _instances[index] = _instances[index] with { Status = status };
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}

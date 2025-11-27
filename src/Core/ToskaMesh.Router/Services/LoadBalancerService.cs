using System.Collections.Concurrent;
using System.Linq;
using ToskaMesh.Protocols;
using ToskaMesh.Telemetry;

namespace ToskaMesh.Router.Services;

/// <summary>
/// Implementation of <see cref="ILoadBalancer"/> supporting multiple strategies.
/// </summary>
public class LoadBalancerService : ILoadBalancer
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly MeshMetrics _metrics;
    private readonly ConcurrentDictionary<string, RoundRobinState> _roundRobin;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _connectionCounts;
    private readonly ConcurrentDictionary<string, ServiceLoadStats> _stats;

    public LoadBalancerService(IServiceRegistry serviceRegistry)
    {
        _serviceRegistry = serviceRegistry;
        _metrics = new MeshMetrics("Router");
        _roundRobin = new ConcurrentDictionary<string, RoundRobinState>(StringComparer.OrdinalIgnoreCase);
        _connectionCounts = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        _stats = new ConcurrentDictionary<string, ServiceLoadStats>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ServiceInstance?> SelectInstanceAsync(string serviceName, LoadBalancingContext context, CancellationToken cancellationToken = default)
    {
        var instances = (await _serviceRegistry.GetServiceInstancesAsync(serviceName, cancellationToken)).ToList();
        var healthy = instances.Where(instance => instance.Status == HealthStatus.Healthy).ToList();

        var candidates = healthy.Count > 0 ? healthy : instances.Where(instance => instance.Status != HealthStatus.Unknown).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var strategy = ResolveStrategy(candidates);
        ServiceInstance? selected = strategy switch
        {
            LoadBalancingStrategy.RoundRobin => SelectRoundRobin(serviceName, candidates),
            LoadBalancingStrategy.LeastConnections => SelectLeastConnections(serviceName, candidates),
            LoadBalancingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(serviceName, candidates),
            LoadBalancingStrategy.IPHash => SelectIpHash(serviceName, candidates, context),
            _ => SelectRoundRobin(serviceName, candidates)
        };

        if (selected != null)
        {
            RecordRequest(serviceName, selected, context);
        }

        return selected;
    }

    public Task ReportResultAsync(string serviceId, RequestResult result, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in _connectionCounts)
        {
            if (kvp.Value.TryGetValue(serviceId, out var current))
            {
                kvp.Value[serviceId] = Math.Max(0, current - 1);
            }
        }

        if (_stats.TryGetValue(result.ServiceId, out var stats))
        {
            stats.Report(result);
        }

        return Task.CompletedTask;
    }

    public Task<LoadBalancingStats> GetStatsAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var serviceStats = _stats.Values
            .Where(stats => stats.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(stats => stats.Snapshot())
            .ToList();

        if (serviceStats.Count == 0)
        {
            return Task.FromResult(new LoadBalancingStats(serviceName, 0, 0, 0, TimeSpan.Zero, new Dictionary<string, int>()));
        }

        var aggregate = serviceStats.Aggregate(new ServiceSummary(), (summary, next) => summary.Add(next));
        var result = new LoadBalancingStats(
            serviceName,
            Convert.ToInt32(aggregate.TotalRequests),
            Convert.ToInt32(aggregate.SuccessfulRequests),
            Convert.ToInt32(aggregate.FailedRequests),
            aggregate.AverageResponseTime,
            aggregate.InstanceCounts.ToDictionary(pair => pair.Key, pair => pair.Value));

        return Task.FromResult(result);
    }

    private LoadBalancingStrategy ResolveStrategy(IReadOnlyList<ServiceInstance> candidates)
    {
        var strategy = candidates.Select(instance => instance.Metadata.GetValueOrDefault("lb_strategy"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (Enum.TryParse(strategy, true, out LoadBalancingStrategy parsed))
        {
            return parsed;
        }

        return LoadBalancingStrategy.RoundRobin;
    }

    private ServiceInstance SelectRoundRobin(string serviceName, IReadOnlyList<ServiceInstance> instances)
    {
        var state = _roundRobin.GetOrAdd(serviceName, _ => new RoundRobinState());
        var index = state.NextIndex(instances.Count);
        return instances[index];
    }

    private ServiceInstance SelectLeastConnections(string serviceName, IReadOnlyList<ServiceInstance> instances)
    {
        var counts = _connectionCounts.GetOrAdd(serviceName, _ => new ConcurrentDictionary<string, int>());
        foreach (var instance in instances)
        {
            counts.TryAdd(instance.ServiceId, 0);
        }

        var selected = instances.OrderBy(instance => counts.GetValueOrDefault(instance.ServiceId)).First();
        counts.AddOrUpdate(selected.ServiceId, 1, (_, value) => value + 1);
        return selected;
    }

    private ServiceInstance SelectWeightedRoundRobin(string serviceName, IReadOnlyList<ServiceInstance> instances)
    {
        var weighted = new List<ServiceInstance>();
        foreach (var instance in instances)
        {
            var weight = 1;
            if (instance.Metadata.TryGetValue("weight", out var weightString) && int.TryParse(weightString, out var parsed) && parsed > 0)
            {
                weight = parsed;
            }

            for (var i = 0; i < weight; i++)
            {
                weighted.Add(instance);
            }
        }

        return SelectRoundRobin(serviceName + "-weighted", weighted);
    }

    private ServiceInstance SelectIpHash(string serviceName, IReadOnlyList<ServiceInstance> instances, LoadBalancingContext context)
    {
        var key = context.SessionId ?? GetHeader(context, "X-Correlation-ID") ?? Guid.NewGuid().ToString();
        var hash = Math.Abs(key.GetHashCode());
        return instances[hash % instances.Count];
    }

    private void RecordRequest(string serviceName, ServiceInstance instance, LoadBalancingContext context)
    {
        var method = GetHeader(context, "method") ?? "proxy";
        _metrics.RecordRequest($"/proxy/{serviceName}", method);

        var serviceStats = _stats.GetOrAdd(instance.ServiceId, _ => new ServiceLoadStats(serviceName));
        serviceStats.RecordRequest(instance.ServiceId);
    }

    private static string? GetHeader(LoadBalancingContext context, string key)
    {
        if (context.Headers != null && context.Headers.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    private sealed class RoundRobinState
    {
        private int _index;

        public int NextIndex(int length)
        {
            var next = Interlocked.Increment(ref _index);
            var idx = Math.Abs(next) % length;
            return idx;
        }
    }

    private sealed class ServiceLoadStats
    {
        private readonly string _serviceName;
        private long _totalRequests;
        private long _successfulRequests;
        private long _failedRequests;
        private long _totalResponseTicks;
        private readonly ConcurrentDictionary<string, int> _instanceCounts = new();

        public ServiceLoadStats(string serviceName)
        {
            _serviceName = serviceName;
        }

        public string ServiceName => _serviceName;

        public void RecordRequest(string instanceId)
        {
            Interlocked.Increment(ref _totalRequests);
            _instanceCounts.AddOrUpdate(instanceId, 1, (_, value) => value + 1);
        }

        public void Report(RequestResult result)
        {
            if (result.Success)
            {
                Interlocked.Increment(ref _successfulRequests);
            }
            else
            {
                Interlocked.Increment(ref _failedRequests);
            }

            Interlocked.Add(ref _totalResponseTicks, result.ResponseTime.Ticks);
        }

        public ServiceSummary Snapshot()
        {
            var total = Interlocked.Read(ref _totalRequests);
            var succeeded = Interlocked.Read(ref _successfulRequests);
            var failed = Interlocked.Read(ref _failedRequests);
            var ticks = Interlocked.Read(ref _totalResponseTicks);
            var counts = _instanceCounts.ToDictionary(pair => pair.Key, pair => pair.Value);

            return new ServiceSummary(total, succeeded, failed, ticks, counts);
        }
    }

    private sealed record ServiceSummary(
        long TotalRequests,
        long SuccessfulRequests,
        long FailedRequests,
        long TotalResponseTicks,
        IReadOnlyDictionary<string, int> InstanceCounts)
    {
        public ServiceSummary() : this(0, 0, 0, 0, new Dictionary<string, int>())
        {
        }

        public ServiceSummary Add(ServiceSummary other)
        {
            var combinedCounts = InstanceCounts.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var pair in other.InstanceCounts)
            {
                combinedCounts[pair.Key] = combinedCounts.GetValueOrDefault(pair.Key) + pair.Value;
            }

            return new ServiceSummary(
                TotalRequests + other.TotalRequests,
                SuccessfulRequests + other.SuccessfulRequests,
                FailedRequests + other.FailedRequests,
                TotalResponseTicks + other.TotalResponseTicks,
                combinedCounts);
        }

        public TimeSpan AverageResponseTime => TotalRequests > 0 ? TimeSpan.FromTicks(TotalResponseTicks / TotalRequests) : TimeSpan.Zero;
    }
}

using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using ToskaMesh.Core.Grains.Models;
using ToskaMesh.Protocols;

namespace ToskaMesh.Core.Grains;

public class ClusterCoordinatorGrain : Grain, IClusterCoordinatorGrain
{
    private readonly IPersistentState<ClusterCoordinatorState> _state;
    private readonly ILogger<ClusterCoordinatorGrain> _logger;
    private readonly ConcurrentDictionary<string, HashSet<IClusterEventObserver>> _topicObservers;

    public ClusterCoordinatorGrain(
        [PersistentState("coordinator", "clusterStore")] IPersistentState<ClusterCoordinatorState> state,
        ILogger<ClusterCoordinatorGrain> logger)
    {
        _state = state;
        _logger = logger;
        _topicObservers = new ConcurrentDictionary<string, HashSet<IClusterEventObserver>>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> JoinClusterAsync(ClusterMemberDto member)
    {
        if (string.IsNullOrWhiteSpace(member.MemberId))
        {
            return false;
        }

        var normalized = member with
        {
            JoinedAt = member.JoinedAt == default ? DateTime.UtcNow : member.JoinedAt,
            Metadata = member.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        _state.State.Members[normalized.MemberId] = normalized.ToState();
        _state.State.LastUpdated = DateTime.UtcNow;
        await _state.WriteStateAsync();

        _logger.LogInformation("Member {MemberId} joined cluster", normalized.MemberId);

        await NotifyObserversAsync("cluster.join", BuildEnvelope("cluster.join", normalized));
        return true;
    }

    public async Task<bool> LeaveClusterAsync(string memberId)
    {
        if (!_state.State.Members.Remove(memberId))
        {
            return false;
        }

        _state.State.LastUpdated = DateTime.UtcNow;
        await _state.WriteStateAsync();

        _logger.LogInformation("Member {MemberId} left cluster", memberId);

        await NotifyObserversAsync("cluster.leave", BuildEnvelope("cluster.leave", new { MemberId = memberId }));
        return true;
    }

    public Task<List<ClusterMemberDto>> GetMembersAsync()
    {
        var members = _state.State.Members.Values
            .Select(state => state.ToDto())
            .ToList();

        return Task.FromResult(members);
    }

    public Task<ClusterMemberDto?> GetMemberAsync(string memberId)
    {
        if (_state.State.Members.TryGetValue(memberId, out var member))
        {
            return Task.FromResult<ClusterMemberDto?>(member.ToDto());
        }

        return Task.FromResult<ClusterMemberDto?>(null);
    }

    public Task<ClusterTopologySnapshot> GetTopologyAsync()
    {
        var members = _state.State.Members.Values
            .Select(state => state.ToDto())
            .ToList();

        var leader = DetermineLeader(members);
        var snapshot = new ClusterTopologySnapshot(
            members.Count,
            leader,
            members,
            _state.State.LastUpdated);

        return Task.FromResult(snapshot);
    }

    public async Task BroadcastAsync(ClusterEventEnvelope envelope)
    {
        await NotifyObserversAsync(envelope.Topic, envelope with { OccurredAt = DateTime.UtcNow });
    }

    public Task SubscribeAsync(string topic, IClusterEventObserver observer)
    {
        var observers = _topicObservers.GetOrAdd(topic, _ => new HashSet<IClusterEventObserver>(ReferenceEqualityComparer.Instance));
        lock (observers)
        {
            observers.Add(observer);
        }

        _logger.LogDebug("Observer subscribed to topic {Topic}", topic);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string topic, IClusterEventObserver observer)
    {
        if (_topicObservers.TryGetValue(topic, out var observers))
        {
            lock (observers)
            {
                observers.Remove(observer);
                if (observers.Count == 0)
                {
                    _topicObservers.TryRemove(topic, out _);
                }
            }
        }

        _logger.LogDebug("Observer unsubscribed from topic {Topic}", topic);
        return Task.CompletedTask;
    }

    private Task NotifyObserversAsync(string topic, ClusterEventEnvelope envelope)
    {
        if (_topicObservers.TryGetValue(topic, out var observers))
        {
            List<IClusterEventObserver> snapshot;
            lock (observers)
            {
                snapshot = observers.ToList();
            }

            foreach (var observer in snapshot)
            {
                try
                {
                    _ = observer.OnEventAsync(envelope);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Observer invocation failed for topic {Topic}", topic);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static string DetermineLeader(IEnumerable<ClusterMemberDto> members)
    {
        var leader = members
            .Where(m => m.Status == NodeStatus.Alive)
            .OrderByDescending(m => m.Metadata.TryGetValue("role", out var role) && string.Equals(role, "leader", StringComparison.OrdinalIgnoreCase))
            .ThenBy(m => m.JoinedAt)
            .ThenBy(m => m.MemberId)
            .FirstOrDefault();

        return leader?.MemberId ?? string.Empty;
    }

    private static ClusterEventEnvelope BuildEnvelope(string topic, object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        return new ClusterEventEnvelope(topic, json, payload.GetType().AssemblyQualifiedName ?? payload.GetType().FullName ?? topic, DateTime.UtcNow);
    }
}

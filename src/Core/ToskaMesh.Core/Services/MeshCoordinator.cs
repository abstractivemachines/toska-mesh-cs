using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orleans;
using ToskaMesh.Core.Grains;
using ToskaMesh.Core.Grains.Models;
using ToskaMesh.Protocols;
using ProtocolClusterMember = ToskaMesh.Protocols.ClusterMember;
using ProtocolClusterTopology = ToskaMesh.Protocols.ClusterTopology;

namespace ToskaMesh.Core.Services;

/// <summary>
/// Orleans-backed implementation of the mesh coordinator API.
/// </summary>
public class MeshCoordinator : IMeshCoordinator, IClusterEventObserver, IAsyncDisposable
{
    private const string CoordinatorGrainId = "cluster";

    private readonly IClusterClient _clusterClient;
    private readonly ILogger<MeshCoordinator> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, TopicSubscription> _topicHandlers;
    private IClusterEventObserver? _observerReference;

    public MeshCoordinator(IClusterClient clusterClient, ILogger<MeshCoordinator> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        _topicHandlers = new ConcurrentDictionary<string, TopicSubscription>(StringComparer.OrdinalIgnoreCase);
    }

    private IClusterCoordinatorGrain Grain => _clusterClient.GetGrain<IClusterCoordinatorGrain>(CoordinatorGrainId);

    public async Task<bool> JoinClusterAsync(ProtocolClusterMember member, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(member);
        return await Grain.JoinClusterAsync(dto);
    }

    public async Task<bool> LeaveClusterAsync(string memberId, CancellationToken cancellationToken = default)
    {
        return await Grain.LeaveClusterAsync(memberId);
    }

    public async Task<IEnumerable<ProtocolClusterMember>> GetClusterMembersAsync(CancellationToken cancellationToken = default)
    {
        var members = await Grain.GetMembersAsync();
        return members.Select(FromDto).ToList();
    }

    public async Task<ProtocolClusterTopology> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await Grain.GetTopologyAsync();
        return new ClusterTopology(
            snapshot.MemberCount,
            snapshot.Leader,
            snapshot.Members.Select(FromDto).ToList(),
            snapshot.LastUpdated);
    }

    public async Task BroadcastAsync<T>(string topic, T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = new ClusterEventEnvelope(
            topic,
            JsonSerializer.Serialize(message, _jsonOptions),
            typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            DateTime.UtcNow);

        await Grain.BroadcastAsync(envelope);
    }

    public async Task SubscribeAsync<T>(string topic, Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        await EnsureObserverAsync();

        var registration = new EventHandlerRegistration(
            typeof(T),
            async envelope =>
            {
                if (!IsMatchingType(envelope, typeof(T)))
                {
                    return;
                }

                var payload = JsonSerializer.Deserialize<T>(envelope.Payload, _jsonOptions);
                if (payload != null)
                {
                    await handler(payload);
                }
            });

        var subscription = _topicHandlers.GetOrAdd(topic, _ => new TopicSubscription());
        subscription.AddHandler(registration);

        if (subscription.EnsureSubscribed())
        {
            await Grain.SubscribeAsync(topic, _observerReference!);
        }
    }

    private Task EnsureObserverAsync()
    {
        if (_observerReference != null)
        {
            return Task.CompletedTask;
        }

        var observer = new ClusterEventObserver(this);
        _observerReference = _clusterClient.CreateObjectReference<IClusterEventObserver>(observer);
        return Task.CompletedTask;
    }

    Task IClusterEventObserver.OnEventAsync(ClusterEventEnvelope envelope)
    {
        DispatchEvent(envelope);
        return Task.CompletedTask;
    }

    private void DispatchEvent(ClusterEventEnvelope envelope)
    {
        if (!_topicHandlers.TryGetValue(envelope.Topic, out var subscription))
        {
            return;
        }

        foreach (var handler in subscription.GetHandlers())
        {
            _ = handler.Invoke(envelope);
        }
    }

    private static bool IsMatchingType(ClusterEventEnvelope envelope, Type type)
    {
        var typeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        return string.Equals(envelope.ContentType, typeName, StringComparison.Ordinal)
               || string.Equals(envelope.ContentType, type.FullName, StringComparison.Ordinal);
    }

    private static ClusterMemberDto ToDto(ProtocolClusterMember member)
    {
        return new ClusterMemberDto(
            member.MemberId,
            member.NodeName,
            member.Address,
            member.Port,
            member.Status,
            member.JoinedAt == default ? DateTime.UtcNow : member.JoinedAt,
            new Dictionary<string, string>(member.Metadata, StringComparer.OrdinalIgnoreCase));
    }

    private static ProtocolClusterMember FromDto(ClusterMemberDto dto)
    {
        return new ProtocolClusterMember(
            dto.MemberId,
            dto.NodeName,
            dto.Address,
            dto.Port,
            dto.Status,
            new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase),
            dto.JoinedAt);
    }

    public ValueTask DisposeAsync()
    {
        if (_observerReference != null)
        {
            _clusterClient.DeleteObjectReference<IClusterEventObserver>(_observerReference);
        }
        return ValueTask.CompletedTask;
    }

    private sealed class EventHandlerRegistration
    {
        public EventHandlerRegistration(Type payloadType, Func<ClusterEventEnvelope, Task> callback)
        {
            PayloadType = payloadType;
            Callback = callback;
        }

        public Type PayloadType { get; }
        public Func<ClusterEventEnvelope, Task> Callback { get; }

        public Task Invoke(ClusterEventEnvelope envelope) => Callback(envelope);
    }

    private sealed class TopicSubscription
    {
        private readonly List<EventHandlerRegistration> _handlers = new();
        private readonly object _gate = new();
        private bool _isSubscribed;

        public void AddHandler(EventHandlerRegistration registration)
        {
            lock (_gate)
            {
                _handlers.Add(registration);
            }
        }

        public IEnumerable<EventHandlerRegistration> GetHandlers()
        {
            lock (_gate)
            {
                return _handlers.ToArray();
            }
        }

        public bool EnsureSubscribed()
        {
            lock (_gate)
            {
                if (_isSubscribed)
                {
                    return false;
                }

                _isSubscribed = true;
                return true;
            }
        }
    }

    private sealed class ClusterEventObserver : IClusterEventObserver
    {
        private readonly MeshCoordinator _parent;

        public ClusterEventObserver(MeshCoordinator parent)
        {
            _parent = parent;
        }

        public Task OnEventAsync(ClusterEventEnvelope envelope)
        {
            _parent.DispatchEvent(envelope);
            return Task.CompletedTask;
        }
    }
}

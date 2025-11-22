using Orleans;
using ToskaMesh.Core.Grains.Models;

namespace ToskaMesh.Core.Grains;

public interface IClusterCoordinatorGrain : IGrainWithStringKey
{
    Task<bool> JoinClusterAsync(ClusterMemberDto member);
    Task<bool> LeaveClusterAsync(string memberId);
    Task<List<ClusterMemberDto>> GetMembersAsync();
    Task<ClusterMemberDto?> GetMemberAsync(string memberId);
    Task<ClusterTopologySnapshot> GetTopologyAsync();
    Task BroadcastAsync(ClusterEventEnvelope envelope);
    Task SubscribeAsync(string topic, IClusterEventObserver observer);
    Task UnsubscribeAsync(string topic, IClusterEventObserver observer);
}

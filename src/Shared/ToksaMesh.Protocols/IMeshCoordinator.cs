namespace ToksaMesh.Protocols;

/// <summary>
/// Central mesh coordinator for cluster management and coordination
/// </summary>
public interface IMeshCoordinator
{
    /// <summary>
    /// Join the mesh cluster
    /// </summary>
    Task<bool> JoinClusterAsync(ClusterMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leave the mesh cluster
    /// </summary>
    Task<bool> LeaveClusterAsync(string memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all cluster members
    /// </summary>
    Task<IEnumerable<ClusterMember>> GetClusterMembersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cluster topology information
    /// </summary>
    Task<ClusterTopology> GetTopologyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a message to all cluster members
    /// </summary>
    Task BroadcastAsync<T>(string topic, T message, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Subscribe to cluster events
    /// </summary>
    Task SubscribeAsync<T>(string topic, Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class;
}

public record ClusterMember(
    string MemberId,
    string NodeName,
    string Address,
    int Port,
    NodeStatus Status,
    Dictionary<string, string> Metadata,
    DateTime JoinedAt);

public record ClusterTopology(
    int MemberCount,
    string Leader,
    IEnumerable<ClusterMember> Members,
    DateTime LastUpdated);

public enum NodeStatus
{
    Unknown,
    Alive,
    Suspect,
    Dead
}

using Orleans;
using ToskaMesh.Protocols;

namespace ToskaMesh.Core.Grains.Models;

[GenerateSerializer]
public sealed class ClusterCoordinatorState
{
    [Id(0)]
    public Dictionary<string, ClusterMemberState> Members { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [Id(1)]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

[GenerateSerializer]
public sealed class ClusterMemberState
{
    [Id(0)]
    public string MemberId { get; set; } = string.Empty;

    [Id(1)]
    public string NodeName { get; set; } = string.Empty;

    [Id(2)]
    public string Address { get; set; } = string.Empty;

    [Id(3)]
    public int Port { get; set; }

    [Id(4)]
    public NodeStatus Status { get; set; }

    [Id(5)]
    public DateTime JoinedAt { get; set; }

    [Id(6)]
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[GenerateSerializer]
public sealed record ClusterMemberDto(
    [property: Id(0)] string MemberId,
    [property: Id(1)] string NodeName,
    [property: Id(2)] string Address,
    [property: Id(3)] int Port,
    [property: Id(4)] NodeStatus Status,
    [property: Id(5)] DateTime JoinedAt,
    [property: Id(6)] IReadOnlyDictionary<string, string> Metadata);

[GenerateSerializer]
public sealed record ClusterTopologySnapshot(
    [property: Id(0)] int MemberCount,
    [property: Id(1)] string Leader,
    [property: Id(2)] IReadOnlyList<ClusterMemberDto> Members,
    [property: Id(3)] DateTime LastUpdated);

[GenerateSerializer]
public sealed record ClusterEventEnvelope(
    [property: Id(0)] string Topic,
    [property: Id(1)] string Payload,
    [property: Id(2)] string ContentType,
    [property: Id(3)] DateTime OccurredAt);

public static class ClusterModelConversions
{
    public static ClusterMemberState ToState(this ClusterMemberDto dto)
    {
        return new ClusterMemberState
        {
            MemberId = dto.MemberId,
            NodeName = dto.NodeName,
            Address = dto.Address,
            Port = dto.Port,
            Status = dto.Status,
            JoinedAt = dto.JoinedAt,
            Metadata = new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase)
        };
    }

    public static ClusterMemberDto ToDto(this ClusterMemberState state)
    {
        return new ClusterMemberDto(
            state.MemberId,
            state.NodeName,
            state.Address,
            state.Port,
            state.Status,
            state.JoinedAt,
            new Dictionary<string, string>(state.Metadata, StringComparer.OrdinalIgnoreCase));
    }
}

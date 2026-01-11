namespace ToskaMesh.ObservabilityService.Models;

public sealed record SpanRecord(
    string Service,
    string Dependency,
    double DurationMs,
    bool IsError);

public sealed record TopologyNode(
    string Id,
    string DisplayName,
    int Traffic,
    double ErrorRate,
    double P95LatencyMs,
    IReadOnlyList<string> Alerts);

public sealed record TopologyEdge(
    string From,
    string To,
    int Traffic,
    double AvgLatencyMs,
    double ErrorRate);

public sealed record TopologyGraph(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<TopologyEdge> Edges);

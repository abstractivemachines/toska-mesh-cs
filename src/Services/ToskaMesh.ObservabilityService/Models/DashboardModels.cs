namespace ToskaMesh.ObservabilityService.Models;

public sealed record ObservabilityPortalEntry(
    string Name,
    string Path,
    string Description);

public sealed record ServiceDashboard(
    string Service,
    MetricSummary? Metrics,
    IReadOnlyList<SloStatus> SloStatuses,
    IReadOnlyList<ReleaseRecord> RecentReleases,
    IReadOnlyList<PlaybookDefinition> Playbooks,
    TopologyGraph Topology);

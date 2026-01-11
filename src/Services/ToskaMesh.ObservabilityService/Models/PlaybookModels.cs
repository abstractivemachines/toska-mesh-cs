namespace ToskaMesh.ObservabilityService.Models;

public sealed record PlaybookStep(
    string Title,
    IReadOnlyList<string> LogSnippets,
    IReadOnlyList<string> MetricsOverlays,
    IReadOnlyList<string> Actions);

public sealed record PlaybookDefinition(
    string Id,
    string Service,
    string Title,
    string Description,
    string Severity,
    IReadOnlyList<string> RelatedReleaseIds,
    IReadOnlyList<PlaybookStep> Steps);

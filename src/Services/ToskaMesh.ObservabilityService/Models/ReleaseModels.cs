namespace ToskaMesh.ObservabilityService.Models;

public sealed record ReleaseRecord(
    string Id,
    string Service,
    string Version,
    string Environment,
    DateTimeOffset DeployedAt,
    string DiffUrl,
    string RollbackUrl,
    string TriggeredBy,
    IReadOnlyList<string> BlastRadiusServices);

public sealed record ReleaseIngestRequest(
    string Service,
    string Version,
    string Environment,
    string DiffUrl,
    string TriggeredBy,
    IReadOnlyList<string> BlastRadiusServices);

public sealed record RollbackResult(
    string ReleaseId,
    string Service,
    string RollbackUrl,
    string Status,
    DateTimeOffset RequestedAt);

using ToskaMesh.Protocols;

namespace ToskaMesh.Gateway.Models;

public sealed record ProxyResponse(string Content, string ContentType, int StatusCode);

public sealed record DashboardServiceCatalogItem(
    string ServiceName,
    IReadOnlyCollection<ServiceInstance> Instances,
    IReadOnlyCollection<DashboardHealthSnapshot> Health,
    DashboardServiceMetadataSummary? Metadata);

public sealed record DashboardHealthSnapshot(
    string ServiceId,
    string ServiceName,
    string Address,
    int Port,
    HealthStatus Status,
    DateTime LastProbe,
    string LastProbeType,
    string? Message,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record DashboardServiceMetadataSummary(
    string ServiceName,
    int InstanceCount,
    DateTime GeneratedAt,
    IReadOnlyList<DashboardMetadataKeySummary> Keys);

public sealed record DashboardMetadataKeySummary(
    string Key,
    int InstanceCount,
    IReadOnlyList<string> Values);

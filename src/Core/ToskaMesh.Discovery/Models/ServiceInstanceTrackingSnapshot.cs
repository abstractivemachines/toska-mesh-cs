using ToskaMesh.Protocols;

namespace ToskaMesh.Discovery.Models;

/// <summary>
/// Snapshot of a service instance's lifecycle and health information.
/// </summary>
public record ServiceInstanceTrackingSnapshot(
    string ServiceId,
    string ServiceName,
    DateTime RegisteredAt,
    DateTime? DeregisteredAt,
    DateTime LastUpdated,
    HealthStatus Status,
    DateTime? LastHealthCheck,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Summary of metadata usage across all instances of a service.
/// </summary>
public record ServiceMetadataSummary(
    string ServiceName,
    int InstanceCount,
    DateTime GeneratedAt,
    IReadOnlyList<MetadataKeySummary> Keys);

/// <summary>
/// Describes how a metadata key is used across service instances.
/// </summary>
public record MetadataKeySummary(
    string Key,
    int InstanceCount,
    IReadOnlyList<string> Values);

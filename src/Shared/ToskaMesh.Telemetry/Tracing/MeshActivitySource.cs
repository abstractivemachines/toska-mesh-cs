using System.Collections.Concurrent;
using System.Diagnostics;

namespace ToskaMesh.Telemetry.Tracing;

/// <summary>
/// Provides a cached <see cref="ActivitySource" /> per service for manual tracing scenarios.
/// </summary>
public static class MeshActivitySource
{
    private static readonly ConcurrentDictionary<string, ActivitySource> Sources = new();

    /// <summary>
    /// Gets (or creates) the activity source for the provided service.
    /// </summary>
    public static ActivitySource Get(string serviceName, string? serviceVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var key = $"{serviceName}:{serviceVersion ?? "1.0.0"}";
        return Sources.GetOrAdd(key, _ =>
            new ActivitySource($"ToskaMesh.{serviceName}", serviceVersion ?? "1.0.0"));
    }
}

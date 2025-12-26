using System.Diagnostics;

namespace ToskaMesh.Telemetry.Tracing;

/// <summary>
/// Helper methods for starting activities with consistent ToskaMesh conventions.
/// </summary>
public static class MeshTracing
{
    /// <summary>
    /// Starts an activity for the requested service with optional tags.
    /// </summary>
    public static Activity? StartActivity(
        string serviceName,
        string activityName,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null,
        string? serviceVersion = null)
    {
        var activitySource = MeshActivitySource.Get(serviceName, serviceVersion);
        var activity = activitySource.StartActivity(activityName, kind);

        if (activity != null && tags != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }

        return activity;
    }
}

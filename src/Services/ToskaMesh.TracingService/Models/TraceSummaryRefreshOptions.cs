namespace ToskaMesh.TracingService.Models;

public sealed class TraceSummaryRefreshOptions
{
    public bool Enabled { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 5;
    public bool RefreshConcurrently { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 300;
}

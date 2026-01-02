namespace ToskaMesh.TracingService.Models;

public sealed class TraceRetentionOptions
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 14;
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 20000;
    public int CommandTimeoutSeconds { get; set; } = 300;
}

namespace ToskaMesh.TracingService.Models;

public sealed class TraceQueryDefaultsOptions
{
    public bool EnableDefaultLookback { get; set; } = true;
    public int DefaultLookbackHours { get; set; } = 24;
    public int TotalTimeoutSeconds { get; set; } = 5;
    public bool IncludeTotalCounts { get; set; } = false;
}

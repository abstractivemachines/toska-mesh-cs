namespace ToskaMesh.Telemetry.Tracing;

public sealed class MeshTelemetryOptions
{
    public const string SectionName = "Mesh:Telemetry";

    public bool EnableConsoleTraceExporter { get; set; } = true;
    public TracingIngestOptions TracingIngest { get; set; } = new();
}

public sealed class TracingIngestOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string Collector { get; set; } = "ToskaMesh.Telemetry";
    public bool UseMeshServiceAuth { get; set; } = true;
    public int ExportTimeoutSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 256;
    public int QueueSize { get; set; } = 2048;
    public int ExportDelayMs { get; set; } = 5000;
}

public sealed class TracingIngestExporterOptions
{
    public TracingIngestExporterOptions(
        string serviceName,
        string serviceVersion,
        TracingIngestOptions ingestOptions)
    {
        ServiceName = serviceName;
        ServiceVersion = serviceVersion;
        Enabled = ingestOptions.Enabled;
        Endpoint = ingestOptions.Endpoint;
        Collector = ingestOptions.Collector;
        UseMeshServiceAuth = ingestOptions.UseMeshServiceAuth;
        ExportTimeoutSeconds = ingestOptions.ExportTimeoutSeconds;
        BatchSize = ingestOptions.BatchSize;
        QueueSize = ingestOptions.QueueSize;
        ExportDelayMs = ingestOptions.ExportDelayMs;
    }

    public string ServiceName { get; }
    public string ServiceVersion { get; }
    public bool Enabled { get; }
    public string Endpoint { get; }
    public string Collector { get; }
    public bool UseMeshServiceAuth { get; }
    public int ExportTimeoutSeconds { get; }
    public int BatchSize { get; }
    public int QueueSize { get; }
    public int ExportDelayMs { get; }
}

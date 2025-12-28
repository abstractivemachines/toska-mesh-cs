namespace ToskaMesh.Telemetry.Tracing;

public sealed class MeshTelemetryOptions
{
    public const string SectionName = "Mesh:Telemetry";

    public bool EnableConsoleTraceExporter { get; set; } = true;
    public TracingSamplingOptions Sampling { get; set; } = new();
    public TracingIngestOptions TracingIngest { get; set; } = new();
}

public sealed class TracingSamplingOptions
{
    public string Strategy { get; set; } = "ParentBasedAlwaysOn";
    public double Ratio { get; set; } = 1.0;
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
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
        Headers = ingestOptions.Headers is { Count: > 0 }
            ? new Dictionary<string, string>(ingestOptions.Headers, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
    public IReadOnlyDictionary<string, string> Headers { get; }
}

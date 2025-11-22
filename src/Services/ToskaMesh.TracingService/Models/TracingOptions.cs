namespace ToskaMesh.TracingService.Models;

public class TracingExporterOptions
{
    public double SamplingRatio { get; set; } = 1.0;
    public bool EnableConsoleExporter { get; set; }
    public JaegerExporterOptions? Jaeger { get; set; }
    public ZipkinExporterOptions? Zipkin { get; set; }
}

public class JaegerExporterOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6831;
}

public class ZipkinExporterOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:9411/api/v2/spans";
}

using Serilog.Core;
using Serilog.Events;

namespace ToskaMesh.Telemetry.Logging;

/// <summary>
/// Adds the configured service name to each log event.
/// </summary>
public sealed class ServiceNameEnricher : ILogEventEnricher
{
    private readonly string _serviceName;

    public ServiceNameEnricher(string serviceName)
    {
        _serviceName = serviceName;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServiceName", _serviceName));
    }
}

using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace ToskaMesh.Telemetry.Logging;

/// <summary>
/// Adds correlation identifiers (Activity.TraceId) to log events.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = Activity.Current?.TraceId.ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("CorrelationId", correlationId));
        }
    }
}

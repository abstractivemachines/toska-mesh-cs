using System.Collections.Concurrent;
using ToskaMesh.Protocols;

namespace ToskaMesh.HealthMonitor.Services;

public class HealthReportCache
{
    private readonly ConcurrentDictionary<string, MonitoredInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<MonitoredInstance> GetAll() => _instances.Values.ToList();

    public IEnumerable<MonitoredInstance> GetByService(string serviceName)
        => _instances.Values.Where(instance => instance.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

    public void Update(ServiceInstance instance, HealthStatus status, string probeType, string? message)
    {
        var monitored = _instances.GetOrAdd(instance.ServiceId, _ => new MonitoredInstance(instance.ServiceId, instance.ServiceName));
        monitored.Update(instance, status, probeType, message);
    }
}

public sealed class MonitoredInstance
{
    private readonly object _gate = new();

    public MonitoredInstance(string serviceId, string serviceName)
    {
        ServiceId = serviceId;
        ServiceName = serviceName;
    }

    public string ServiceId { get; }
    public string ServiceName { get; }
    public string Address { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public HealthStatus Status { get; private set; } = HealthStatus.Unknown;
    public DateTime LastProbe { get; private set; }
    public string LastProbeType { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public IReadOnlyDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>();

    public void Update(ServiceInstance instance, HealthStatus status, string probeType, string? message)
    {
        lock (_gate)
        {
            Address = instance.Address;
            Port = instance.Port;
            Metadata = instance.Metadata;
            Status = status;
            LastProbe = DateTime.UtcNow;
            LastProbeType = probeType;
            Message = message;
        }
    }
}

namespace ToskaMesh.Runtime.Stateful;

/// <summary>
/// Key/value configuration for stateful services.
/// </summary>
public class StatefulKeyValueOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }
    public string? KeyPrefix { get; set; }
    public int? Database { get; set; }

    internal void EnsureDefaults(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            KeyPrefix = serviceName;
        }
    }
}

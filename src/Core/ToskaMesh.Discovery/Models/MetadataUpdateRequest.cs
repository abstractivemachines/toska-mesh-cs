namespace ToskaMesh.Discovery.Models;

/// <summary>
/// Request payload for updating metadata associated with a service instance.
/// </summary>
public class MetadataUpdateRequest
{
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

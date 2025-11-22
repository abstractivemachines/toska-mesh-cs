namespace ToskaMesh.ConfigService.Entities;

public class ConfigurationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Environment { get; set; } = "default";
    public string Version { get; set; } = "1.0.0";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
    public ICollection<ConfigurationVersion> History { get; set; } = new List<ConfigurationVersion>();
}

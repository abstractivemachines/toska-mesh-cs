namespace ToskaMesh.ConfigService.Entities;

public class ConfigurationVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConfigurationItemId { get; set; }
    public ConfigurationItem ConfigurationItem { get; set; } = default!;
    public string Version { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public string? Notes { get; set; }
}

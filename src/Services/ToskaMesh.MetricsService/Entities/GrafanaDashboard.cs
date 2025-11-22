namespace ToskaMesh.MetricsService.Entities;

public class GrafanaDashboard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Folder { get; set; } = "ToskaMesh";
    public string Datasource { get; set; } = "Prometheus";
    public string JsonDefinition { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

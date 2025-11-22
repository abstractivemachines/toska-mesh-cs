using System.ComponentModel.DataAnnotations;

namespace ToskaMesh.MetricsService.Models;

public record GrafanaDashboardDto(
    Guid Id,
    string Title,
    string Uid,
    string Folder,
    string Datasource,
    string JsonDefinition,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record GrafanaDashboardRequest
{
    [Required]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Uid { get; init; } = string.Empty;

    public string Folder { get; init; } = "ToskaMesh";

    public string Datasource { get; init; } = "Prometheus";

    [Required]
    public string JsonDefinition { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;
}

public record GrafanaProvisioningDocument(string ApiVersion, IReadOnlyCollection<GrafanaProvisionedDashboard> Dashboards);

public record GrafanaProvisionedDashboard(
    string Uid,
    string Title,
    string Folder,
    string Datasource,
    string JsonDefinition);

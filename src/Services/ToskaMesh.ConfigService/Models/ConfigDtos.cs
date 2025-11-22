namespace ToskaMesh.ConfigService.Models;

public record ConfigurationUpsertRequest(
    string Name,
    string Environment,
    string Content,
    string? Description,
    string? Notes);

public record ConfigurationResponse(
    Guid Id,
    string Name,
    string Environment,
    string Version,
    string Content,
    string? Description,
    DateTime CreatedAt,
    string CreatedBy);

public record ConfigurationSummary(
    Guid Id,
    string Name,
    string Environment,
    string Version,
    bool Active,
    DateTime CreatedAt);

public record RollbackRequest(string Version, string? Notes);

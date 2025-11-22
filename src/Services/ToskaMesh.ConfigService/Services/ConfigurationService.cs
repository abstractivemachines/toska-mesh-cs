using Microsoft.EntityFrameworkCore;
using ToskaMesh.ConfigService.Data;
using ToskaMesh.ConfigService.Entities;
using ToskaMesh.ConfigService.Models;

namespace ToskaMesh.ConfigService.Services;

public interface IConfigurationService
{
    Task<IEnumerable<ConfigurationSummary>> GetAllAsync(string? environment, CancellationToken cancellationToken = default);
    Task<ConfigurationResponse?> GetAsync(string name, string environment, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConfigurationVersion>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ConfigurationResponse> UpsertAsync(ConfigurationUpsertRequest request, string user, CancellationToken cancellationToken = default);
    Task<bool> RollbackAsync(Guid id, RollbackRequest request, string user, CancellationToken cancellationToken = default);
}

public class ConfigurationService : IConfigurationService
{
    private readonly ConfigDbContext _dbContext;
    private readonly IYamlValidationService _yamlValidator;

    public ConfigurationService(ConfigDbContext dbContext, IYamlValidationService yamlValidator)
    {
        _dbContext = dbContext;
        _yamlValidator = yamlValidator;
    }

    public async Task<IEnumerable<ConfigurationSummary>> GetAllAsync(string? environment, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Configurations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(config => config.Environment == environment);
        }

        return await query
            .OrderBy(config => config.Name)
            .Select(config => new ConfigurationSummary(config.Id, config.Name, config.Environment, config.Version, config.Active, config.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResponse?> GetAsync(string name, string environment, CancellationToken cancellationToken = default)
    {
        var config = await _dbContext.Configurations
            .FirstOrDefaultAsync(item => item.Name == name && item.Environment == environment, cancellationToken);

        return config == null ? null : ToResponse(config);
    }

    public async Task<IEnumerable<ConfigurationVersion>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Versions
            .Where(version => version.ConfigurationItemId == id)
            .OrderByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResponse> UpsertAsync(ConfigurationUpsertRequest request, string user, CancellationToken cancellationToken = default)
    {
        if (!_yamlValidator.TryValidate(request.Content, out var error))
        {
            throw new InvalidOperationException($"Invalid YAML: {error}");
        }

        var config = await _dbContext.Configurations
            .Include(item => item.History)
            .FirstOrDefaultAsync(item => item.Name == request.Name && item.Environment == request.Environment, cancellationToken);

        if (config == null)
        {
            config = new ConfigurationItem
            {
                Name = request.Name,
                Environment = request.Environment,
                Description = request.Description,
                Content = request.Content,
                CreatedBy = user,
                Version = "1.0.0"
            };
            _dbContext.Configurations.Add(config);
        }
        else
        {
            // Save current version in history
            var history = new ConfigurationVersion
            {
                ConfigurationItemId = config.Id,
                Version = config.Version,
                Content = config.Content,
                CreatedAt = config.CreatedAt,
                CreatedBy = config.CreatedBy,
                Notes = request.Notes
            };
            _dbContext.Versions.Add(history);

            config.Version = IncrementVersion(config.Version);
            config.Content = request.Content;
            config.Description = request.Description;
            config.CreatedBy = user;
            config.CreatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(config);
    }

    public async Task<bool> RollbackAsync(Guid id, RollbackRequest request, string user, CancellationToken cancellationToken = default)
    {
        var config = await _dbContext.Configurations
            .Include(item => item.History)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (config == null)
        {
            return false;
        }

        var targetVersion = config.History.FirstOrDefault(version => version.Version == request.Version);
        if (targetVersion == null)
        {
            return false;
        }

        var history = new ConfigurationVersion
        {
            ConfigurationItemId = config.Id,
            Version = config.Version,
            Content = config.Content,
            CreatedAt = config.CreatedAt,
            CreatedBy = config.CreatedBy,
            Notes = request.Notes
        };
        _dbContext.Versions.Add(history);

        config.Version = targetVersion.Version;
        config.Content = targetVersion.Content;
        config.CreatedAt = DateTime.UtcNow;
        config.CreatedBy = user;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string IncrementVersion(string version)
    {
        if (Version.TryParse(version, out var parsed))
        {
            var next = new Version(parsed.Major, parsed.Minor, parsed.Build + 1);
            return next.ToString();
        }

        return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    }

    private static ConfigurationResponse ToResponse(ConfigurationItem config)
    {
        return new ConfigurationResponse(config.Id, config.Name, config.Environment, config.Version, config.Content, config.Description, config.CreatedAt, config.CreatedBy);
    }
}

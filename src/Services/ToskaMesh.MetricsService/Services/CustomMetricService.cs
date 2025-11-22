using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Prometheus;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Entities;
using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Services;

public interface ICustomMetricService
{
    Task<CustomMetricDefinition> RegisterAsync(RegisterCustomMetricRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CustomMetricDefinition>> ListAsync(CancellationToken cancellationToken);
    Task<CustomMetricDefinition> GetRequiredAsync(string name, CancellationToken cancellationToken);
}

public class CustomMetricService : ICustomMetricService
{
    private readonly MetricsDbContext _dbContext;
    private readonly IMetricsRegistry _metricsRegistry;
    private readonly IMemoryCache _memoryCache;
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public CustomMetricService(
        MetricsDbContext dbContext,
        IMetricsRegistry metricsRegistry,
        IMemoryCache memoryCache)
    {
        _dbContext = dbContext;
        _metricsRegistry = metricsRegistry;
        _memoryCache = memoryCache;
    }

    public async Task<CustomMetricDefinition> RegisterAsync(RegisterCustomMetricRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedName = request.Name.Trim();
        var exists = await _dbContext.CustomMetrics.AnyAsync(metric => metric.Name == normalizedName, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Metric '{normalizedName}' is already registered.");
        }

        var definition = new CustomMetricDefinition
        {
            Name = normalizedName,
            HelpText = request.HelpText,
            Type = request.Type,
            LabelNames = request.LabelNames is { Length: > 0 }
                ? request.LabelNames.Select(label => label.Trim()).ToArray()
                : null,
            Unit = request.Unit,
            IsVisible = request.IsVisible
        };

        _dbContext.CustomMetrics.Add(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _metricsRegistry.RegisterCustomMetric(definition);
        _memoryCache.Set(definition.Name, definition, CacheOptions);
        return definition;
    }

    public async Task<IReadOnlyCollection<CustomMetricDefinition>> ListAsync(CancellationToken cancellationToken)
    {
        var metrics = await _dbContext.CustomMetrics
            .AsNoTracking()
            .OrderBy(metric => metric.Name)
            .ToListAsync(cancellationToken);

        foreach (var definition in metrics)
        {
            _memoryCache.Set(definition.Name, definition, CacheOptions);
        }

        return metrics;
    }

    public async Task<CustomMetricDefinition> GetRequiredAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Metric name is required.", nameof(name));
        }

        if (_memoryCache.TryGetValue(name, out CustomMetricDefinition? cached) && cached is not null)
        {
            return cached;
        }

        var definition = await _dbContext.CustomMetrics.AsNoTracking()
            .SingleOrDefaultAsync(metric => metric.Name == name, cancellationToken)
            ?? throw new KeyNotFoundException($"Metric '{name}' is not registered.");

        _metricsRegistry.RegisterCustomMetric(definition);
        _memoryCache.Set(definition.Name, definition, CacheOptions);
        return definition;
    }
}

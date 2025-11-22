using Microsoft.EntityFrameworkCore;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Entities;
using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Services;

public interface IGrafanaProvisioningService
{
    Task<GrafanaDashboardDto> CreateAsync(GrafanaDashboardRequest request, CancellationToken cancellationToken);
    Task<GrafanaDashboardDto> UpdateAsync(Guid id, GrafanaDashboardRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<GrafanaDashboardDto>> ListAsync(CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<GrafanaProvisioningDocument> BuildProvisioningDocumentAsync(CancellationToken cancellationToken);
}

public class GrafanaProvisioningService : IGrafanaProvisioningService
{
    private readonly MetricsDbContext _dbContext;

    public GrafanaProvisioningService(MetricsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GrafanaDashboardDto> CreateAsync(GrafanaDashboardRequest request, CancellationToken cancellationToken)
    {
        var normalizedUid = request.Uid.Trim();

        var exists = await _dbContext.GrafanaDashboards.AnyAsync(dashboard => dashboard.Uid == normalizedUid, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"Grafana dashboard '{normalizedUid}' already exists.");
        }

        var entity = new GrafanaDashboard
        {
            Title = request.Title,
            Uid = normalizedUid,
            Folder = request.Folder,
            Datasource = request.Datasource,
            JsonDefinition = request.JsonDefinition,
            Enabled = request.Enabled
        };

        _dbContext.GrafanaDashboards.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<GrafanaDashboardDto> UpdateAsync(Guid id, GrafanaDashboardRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.GrafanaDashboards.SingleOrDefaultAsync(dashboard => dashboard.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Grafana dashboard '{id}' was not found.");

        entity.Title = request.Title;
        entity.Uid = request.Uid;
        entity.Folder = request.Folder;
        entity.Datasource = request.Datasource;
        entity.JsonDefinition = request.JsonDefinition;
        entity.Enabled = request.Enabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyCollection<GrafanaDashboardDto>> ListAsync(CancellationToken cancellationToken)
    {
        var dashboards = await _dbContext.GrafanaDashboards.AsNoTracking()
            .OrderBy(d => d.Title)
            .ToListAsync(cancellationToken);

        return dashboards.Select(ToDto).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.GrafanaDashboards.SingleOrDefaultAsync(dashboard => dashboard.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Grafana dashboard '{id}' was not found.");

        _dbContext.GrafanaDashboards.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<GrafanaProvisioningDocument> BuildProvisioningDocumentAsync(CancellationToken cancellationToken)
    {
        var dashboards = await _dbContext.GrafanaDashboards.AsNoTracking()
            .Where(d => d.Enabled)
            .OrderBy(d => d.Title)
            .ToListAsync(cancellationToken);

        var provisioned = dashboards
            .Select(d => new GrafanaProvisionedDashboard(d.Uid, d.Title, d.Folder, d.Datasource, d.JsonDefinition))
            .ToList();

        return new GrafanaProvisioningDocument("v1", provisioned);
    }

    private static GrafanaDashboardDto ToDto(GrafanaDashboard entity) =>
        new(
            entity.Id,
            entity.Title,
            entity.Uid,
            entity.Folder,
            entity.Datasource,
            entity.JsonDefinition,
            entity.Enabled,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}

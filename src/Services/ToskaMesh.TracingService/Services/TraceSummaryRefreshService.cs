using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Models;

namespace ToskaMesh.TracingService.Services;

public class TraceSummaryRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TraceSummaryRefreshService> _logger;
    private readonly TraceSummaryRefreshOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public TraceSummaryRefreshService(
        IServiceScopeFactory scopeFactory,
        IOptions<TraceSummaryRefreshOptions> options,
        ILogger<TraceSummaryRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value ?? new TraceSummaryRefreshOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Trace summary refresh is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.RefreshIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshOnceAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _refreshLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TracingDbContext>();
            var originalTimeout = dbContext.Database.GetCommandTimeout();
            if (_options.CommandTimeoutSeconds > 0)
            {
                dbContext.Database.SetCommandTimeout(_options.CommandTimeoutSeconds);
            }
            var refreshSql = _options.RefreshConcurrently
                ? "REFRESH MATERIALIZED VIEW CONCURRENTLY \"TraceSummaries\";"
                : "REFRESH MATERIALIZED VIEW \"TraceSummaries\";";
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(refreshSql, cancellationToken);
            }
            finally
            {
                dbContext.Database.SetCommandTimeout(originalTimeout);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh trace summaries.");
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Models;

namespace ToskaMesh.TracingService.Services;

public sealed class TraceRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TraceRetentionService> _logger;
    private readonly TraceRetentionOptions _options;
    private readonly SemaphoreSlim _retentionLock = new(1, 1);

    public TraceRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptions<TraceRetentionOptions> options,
        ILogger<TraceRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value ?? new TraceRetentionOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Trace retention is disabled.");
            return;
        }

        if (_options.RetentionDays <= 0)
        {
            _logger.LogWarning("Trace retention is enabled but RetentionDays is not set. Skipping cleanup.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CleanupIntervalMinutes));
        _logger.LogInformation(
            "Trace retention is enabled: keeping {RetentionDays} days with cleanup every {IntervalMinutes} minutes.",
            _options.RetentionDays,
            interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PruneOnceAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PruneOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _retentionLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            var batchSize = Math.Max(1, _options.BatchSize);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TracingDbContext>();
            var originalTimeout = dbContext.Database.GetCommandTimeout();
            if (_options.CommandTimeoutSeconds > 0)
            {
                dbContext.Database.SetCommandTimeout(_options.CommandTimeoutSeconds);
            }

            try
            {
                var totalDeleted = 0;
                int deleted;

                do
                {
                    deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM ""TraceSpans""
WHERE ""Id"" IN (
    SELECT ""Id""
    FROM ""TraceSpans""
    WHERE ""EndTimeUtc"" < {cutoff}
    ORDER BY ""EndTimeUtc""
    LIMIT {batchSize}
);", cancellationToken);

                    totalDeleted += deleted;
                }
                while (deleted == batchSize && !cancellationToken.IsCancellationRequested);

                if (totalDeleted > 0)
                {
                    _logger.LogInformation(
                        "Trace retention removed {DeletedCount} spans older than {Cutoff}.",
                        totalDeleted,
                        cutoff);
                }
            }
            finally
            {
                dbContext.Database.SetCommandTimeout(originalTimeout);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply trace retention policy.");
        }
        finally
        {
            _retentionLock.Release();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ToskaMesh.MetricsService.Services;

public class MetricDefinitionWarmupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricDefinitionWarmupService> _logger;

    public MetricDefinitionWarmupService(IServiceProvider serviceProvider, ILogger<MetricDefinitionWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var customMetricService = scope.ServiceProvider.GetRequiredService<ICustomMetricService>();
        var metricsRegistry = scope.ServiceProvider.GetRequiredService<IMetricsRegistry>();

        var definitions = await customMetricService.ListAsync(cancellationToken);
        foreach (var definition in definitions)
        {
            metricsRegistry.RegisterCustomMetric(definition);
        }

        _logger.LogInformation("Loaded {Count} custom metric definitions at startup.", definitions.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

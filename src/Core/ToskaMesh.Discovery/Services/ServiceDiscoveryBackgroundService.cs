namespace ToskaMesh.Discovery.Services;

/// <summary>
/// Background service that periodically performs health checks on registered services.
/// </summary>
public class ServiceDiscoveryBackgroundService : BackgroundService
{
    private readonly IServiceManager _serviceManager;
    private readonly ILogger<ServiceDiscoveryBackgroundService> _logger;
    private readonly TimeSpan _healthCheckInterval;

    public ServiceDiscoveryBackgroundService(
        IServiceManager serviceManager,
        ILogger<ServiceDiscoveryBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceManager = serviceManager;
        _logger = logger;
        _healthCheckInterval = TimeSpan.FromSeconds(
            configuration.GetValue<int>("HealthCheck:IntervalSeconds", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service Discovery Background Service starting. Health check interval: {Interval}",
            _healthCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _serviceManager.PerformHealthChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health checks");
            }

            await Task.Delay(_healthCheckInterval, stoppingToken);
        }

        _logger.LogInformation("Service Discovery Background Service stopping");
    }
}

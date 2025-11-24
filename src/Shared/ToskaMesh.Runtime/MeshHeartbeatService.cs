using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToskaMesh.Protocols;

namespace ToskaMesh.Runtime;

/// <summary>
/// Background service that renews service health/TTL with the registry.
/// </summary>
public class MeshHeartbeatService : BackgroundService
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly MeshServiceOptions _options;
    private readonly MeshRegistrationState _state;
    private readonly ILogger<MeshHeartbeatService> _logger;

    public MeshHeartbeatService(
        IServiceRegistry serviceRegistry,
        MeshServiceOptions options,
        MeshRegistrationState state,
        ILogger<MeshHeartbeatService> logger)
    {
        _serviceRegistry = serviceRegistry;
        _options = options;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.HeartbeatEnabled)
        {
            return;
        }

        var delay = _options.HealthInterval <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(30)
            : _options.HealthInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var id = _state.ServiceId;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    await _serviceRegistry.UpdateHealthStatusAsync(id, HealthStatus.Healthy, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to renew heartbeat for service {ServiceName}", _options.ServiceName);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }
}

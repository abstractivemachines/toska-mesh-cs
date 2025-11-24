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
    private readonly TimeSpan _backoff;
    private readonly int _maxRetries = 3;

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
        _backoff = options.HealthInterval > TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(Math.Max(50, options.HealthInterval.TotalMilliseconds))
            : TimeSpan.FromSeconds(1);
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

        var timer = new PeriodicTimer(delay);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var id = _state.ServiceId;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    await RenewWithRetryAsync(id, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to renew heartbeat for service {ServiceName}", _options.ServiceName);
            }

            if (!await SafeWaitAsync(timer, stoppingToken))
            {
                break;
            }
        }

        // Attempt deregistration health update on shutdown
        if (!string.IsNullOrWhiteSpace(_state.ServiceId))
        {
            try
            {
                await _serviceRegistry.UpdateHealthStatusAsync(_state.ServiceId, HealthStatus.Degraded, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark service {ServiceId} degraded on shutdown", _state.ServiceId);
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RenewWithRetryAsync(string serviceId, CancellationToken token)
    {
        var attempt = 0;
        while (attempt < _maxRetries && !token.IsCancellationRequested)
        {
            try
            {
                await _serviceRegistry.UpdateHealthStatusAsync(serviceId, HealthStatus.Healthy, token);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                _logger.LogWarning(ex, "Heartbeat renew attempt {Attempt}/{Max} failed for {ServiceId}", attempt, _maxRetries, serviceId);
                if (attempt >= _maxRetries)
                {
                    _logger.LogError("Heartbeat renew failed after {Max} attempts for {ServiceId}", _maxRetries, serviceId);
                    return;
                }
                try
                {
                    await Task.Delay(_backoff, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

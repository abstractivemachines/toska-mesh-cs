using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using ToskaMesh.HealthMonitor.Configuration;
using ToskaMesh.Protocols;

namespace ToskaMesh.HealthMonitor.Services;

public class HealthProbeWorker : BackgroundService
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly HealthReportCache _cache;
    private readonly ILogger<HealthProbeWorker> _logger;
    private readonly HealthMonitorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, AsyncCircuitBreakerPolicy> _breakers = new();

    public HealthProbeWorker(
        IServiceRegistry serviceRegistry,
        HealthReportCache cache,
        IOptions<HealthMonitorOptions> options,
        ILogger<HealthProbeWorker> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _serviceRegistry = serviceRegistry;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.HttpTimeoutSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health probe worker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var services = await _serviceRegistry.GetAllServicesAsync(stoppingToken);
                foreach (var serviceName in services)
                {
                    var instances = await _serviceRegistry.GetServiceInstancesAsync(serviceName, stoppingToken);
                    foreach (var instance in instances)
                    {
                        await ProbeInstanceAsync(instance, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health probing cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.ProbeIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProbeInstanceAsync(ServiceInstance instance, CancellationToken cancellationToken)
    {
        var breaker = _breakers.GetValueOrDefault(instance.ServiceId) ?? CreateBreaker(instance.ServiceId);

        try
        {
            await breaker.ExecuteAsync(async ct =>
            {
                var status = await RunHttpProbe(instance, ct) ?? await RunTcpProbe(instance, ct) ?? HealthStatus.Unknown;
                _cache.Update(instance, status, status == HealthStatus.Healthy ? "http/tcp" : "fallback", null);
            }, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _cache.Update(instance, HealthStatus.Unhealthy, "circuit-breaker", "Circuit open due to repeated failures");
        }
        catch (Exception ex)
        {
            _cache.Update(instance, HealthStatus.Unhealthy, "exception", ex.Message);
        }
    }

    private AsyncCircuitBreakerPolicy CreateBreaker(string serviceId)
    {
        var policy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(_options.FailureThreshold, TimeSpan.FromSeconds(_options.ProbeIntervalSeconds * 2));

        _breakers[serviceId] = policy;
        return policy;
    }

    private async Task<HealthStatus?> RunHttpProbe(ServiceInstance instance, CancellationToken cancellationToken)
    {
        if (!instance.Metadata.TryGetValue("health_check_endpoint", out var endpoint))
        {
            return null;
        }

        var scheme = instance.Metadata.GetValueOrDefault("scheme", "http");
        var url = $"{scheme}://{instance.Address}:{instance.Port}{endpoint}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in _options.HttpHeaders)
        {
            var parts = header.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                request.Headers.TryAddWithoutValidation(parts[0], parts[1]);
            }
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var status = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        _cache.Update(instance, status, "http", $"HTTP {(int)response.StatusCode}");
        return status;
    }

    private async Task<HealthStatus?> RunTcpProbe(ServiceInstance instance, CancellationToken cancellationToken)
    {
        if (!instance.Metadata.TryGetValue("tcp_port", out var portString) || !int.TryParse(portString, out var port))
        {
            return null;
        }

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(instance.Address, port);
        var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(_options.TcpTimeoutSeconds), cancellationToken));
        if (completed != connectTask)
        {
            _cache.Update(instance, HealthStatus.Unhealthy, "tcp", "Connection timed out");
            return HealthStatus.Unhealthy;
        }

        await connectTask;

        if (client.Connected)
        {
            _cache.Update(instance, HealthStatus.Healthy, "tcp", "TCP connection successful");
            return HealthStatus.Healthy;
        }

        _cache.Update(instance, HealthStatus.Unhealthy, "tcp", "Connection failed");
        return HealthStatus.Unhealthy;
    }
}

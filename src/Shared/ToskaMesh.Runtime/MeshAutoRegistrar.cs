using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToskaMesh.Protocols;

namespace ToskaMesh.Runtime;

/// <summary>
/// Registers the service instance with the mesh discovery service on startup and deregisters on shutdown.
/// </summary>
public class MeshAutoRegistrar : IHostedService
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly MeshServiceOptions _options;
    private readonly ILogger<MeshAutoRegistrar> _logger;
    private string? _registeredServiceId;

    public MeshAutoRegistrar(
        IServiceRegistry serviceRegistry,
        MeshServiceOptions options,
        ILogger<MeshAutoRegistrar> logger)
    {
        _serviceRegistry = serviceRegistry;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.RegisterAutomatically)
        {
            _logger.LogInformation("Mesh auto-registration disabled for {Service}", _options.ServiceName);
            return;
        }

        var registration = _options.ToRegistration();
        _logger.LogInformation(
            "Registering service {ServiceName} ({ServiceId}) at {Address}:{Port}",
            registration.ServiceName,
            registration.ServiceId,
            registration.Address,
            registration.Port);

        var result = await _serviceRegistry.RegisterAsync(registration, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Service registration failed for {ServiceId}: {Error}",
                registration.ServiceId,
                result.ErrorMessage ?? "unknown error");
            return;
        }

        _registeredServiceId = result.ServiceId;
        _logger.LogInformation("Service registered with ID {ServiceId}", _registeredServiceId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_registeredServiceId))
        {
            return;
        }

        try
        {
            await _serviceRegistry.DeregisterAsync(_registeredServiceId, cancellationToken);
            _logger.LogInformation("Service deregistered: {ServiceId}", _registeredServiceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deregister service {ServiceId}", _registeredServiceId);
        }
    }
}

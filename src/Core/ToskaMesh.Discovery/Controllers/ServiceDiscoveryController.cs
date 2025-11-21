using Microsoft.AspNetCore.Mvc;
using ToskaMesh.Common;
using ToskaMesh.Discovery.Services;
using ToskaMesh.Protocols;

namespace ToskaMesh.Discovery.Controllers;

/// <summary>
/// Controller for service discovery operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServiceDiscoveryController : ControllerBase
{
    private readonly IServiceManager _serviceManager;
    private readonly ILogger<ServiceDiscoveryController> _logger;

    public ServiceDiscoveryController(
        IServiceManager serviceManager,
        ILogger<ServiceDiscoveryController> logger)
    {
        _serviceManager = serviceManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets all registered service names.
    /// </summary>
    [HttpGet("services")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), 200)]
    public async Task<IActionResult> GetServices(CancellationToken cancellationToken)
    {
        var services = await _serviceManager.GetServiceNamesAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<string>>.SuccessResponse(services));
    }

    /// <summary>
    /// Gets all instances of a specific service.
    /// </summary>
    [HttpGet("services/{serviceName}/instances")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ServiceInstance>>), 200)]
    public async Task<IActionResult> GetServiceInstances(string serviceName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting instances for service: {ServiceName}", serviceName);

        var instances = await _serviceManager.GetInstancesAsync(serviceName, cancellationToken);
        return Ok(ApiResponse<IEnumerable<ServiceInstance>>.SuccessResponse(instances));
    }

    /// <summary>
    /// Gets a specific service instance by ID.
    /// </summary>
    [HttpGet("instances/{serviceId}")]
    [ProducesResponseType(typeof(ApiResponse<ServiceInstance>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetServiceInstance(string serviceId, CancellationToken cancellationToken)
    {
        var instance = await _serviceManager.GetInstanceAsync(serviceId, cancellationToken);

        if (instance == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Service instance not found"));
        }

        return Ok(ApiResponse<ServiceInstance>.SuccessResponse(instance));
    }

    /// <summary>
    /// Gets healthy instances of a service (for load balancing).
    /// </summary>
    [HttpGet("services/{serviceName}/instances/healthy")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ServiceInstance>>), 200)]
    public async Task<IActionResult> GetHealthyInstances(string serviceName, CancellationToken cancellationToken)
    {
        var instances = await _serviceManager.GetInstancesAsync(serviceName, cancellationToken);
        var healthyInstances = instances.Where(i => i.Status == HealthStatus.Healthy);

        return Ok(ApiResponse<IEnumerable<ServiceInstance>>.SuccessResponse(healthyInstances));
    }
}

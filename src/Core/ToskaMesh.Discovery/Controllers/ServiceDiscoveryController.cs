using Microsoft.AspNetCore.Mvc;
using ToskaMesh.Common;
using ToskaMesh.Discovery.Models;
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

    /// <summary>
    /// Gets tracking information for a specific instance.
    /// </summary>
    [HttpGet("instances/{serviceId}/tracking")]
    [ProducesResponseType(typeof(ApiResponse<ServiceInstanceTrackingSnapshot>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetInstanceTracking(string serviceId, CancellationToken cancellationToken)
    {
        var tracking = await _serviceManager.GetTrackingAsync(serviceId, cancellationToken);
        if (tracking == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Service instance not found"));
        }

        return Ok(ApiResponse<ServiceInstanceTrackingSnapshot>.SuccessResponse(tracking));
    }

    /// <summary>
    /// Gets tracking snapshots for all instances of a service.
    /// </summary>
    [HttpGet("services/{serviceName}/instances/tracking")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ServiceInstanceTrackingSnapshot>>), 200)]
    public async Task<IActionResult> GetServiceTracking(string serviceName, CancellationToken cancellationToken)
    {
        var snapshots = await _serviceManager.GetTrackingForServiceAsync(serviceName, cancellationToken);
        return Ok(ApiResponse<IEnumerable<ServiceInstanceTrackingSnapshot>>.SuccessResponse(snapshots));
    }

    /// <summary>
    /// Gets metadata summary for a service.
    /// </summary>
    [HttpGet("services/{serviceName}/metadata")]
    [ProducesResponseType(typeof(ApiResponse<ServiceMetadataSummary>), 200)]
    public async Task<IActionResult> GetMetadataSummary(string serviceName, CancellationToken cancellationToken)
    {
        var summary = await _serviceManager.GetMetadataSummaryAsync(serviceName, cancellationToken);
        return Ok(ApiResponse<ServiceMetadataSummary>.SuccessResponse(summary));
    }
}

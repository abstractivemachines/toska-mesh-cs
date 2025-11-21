using Microsoft.AspNetCore.Mvc;
using ToskaMesh.Common;
using ToskaMesh.Discovery.Services;
using ToskaMesh.Protocols;

namespace ToskaMesh.Discovery.Controllers;

/// <summary>
/// Controller for service registration and deregistration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServiceRegistrationController : ControllerBase
{
    private readonly IServiceManager _serviceManager;
    private readonly ILogger<ServiceRegistrationController> _logger;

    public ServiceRegistrationController(
        IServiceManager serviceManager,
        ILogger<ServiceRegistrationController> logger)
    {
        _serviceManager = serviceManager;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new service instance.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<ServiceInstance>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Register([FromBody] ServiceRegistration registration, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received registration request for service: {ServiceName}",
            registration.ServiceName);

        var result = await _serviceManager.RegisterAsync(registration, cancellationToken);

        if (result)
        {
            var instance = await _serviceManager.GetInstanceAsync(registration.ServiceId, cancellationToken);
            return Ok(ApiResponse<ServiceInstance>.SuccessResponse(instance, "Service registered successfully"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse("Failed to register service"));
    }

    /// <summary>
    /// Deregisters a service instance.
    /// </summary>
    [HttpPost("deregister/{serviceId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Deregister(string serviceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received deregistration request for service: {ServiceId}", serviceId);

        var result = await _serviceManager.DeregisterAsync(serviceId, cancellationToken);

        if (result)
        {
            return Ok(ApiResponse<object>.SuccessResponse(null, "Service deregistered successfully"));
        }

        return NotFound(ApiResponse<object>.ErrorResponse("Service not found"));
    }

    /// <summary>
    /// Updates the health status of a service.
    /// </summary>
    [HttpPost("health/{serviceId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> UpdateHealth(
        string serviceId,
        [FromBody] HealthUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _serviceManager.UpdateHealthAsync(
            serviceId,
            request.Status,
            request.Output,
            cancellationToken);

        if (result)
        {
            return Ok(ApiResponse<object>.SuccessResponse(null, "Health status updated"));
        }

        return NotFound(ApiResponse<object>.ErrorResponse("Service not found"));
    }
}

/// <summary>
/// Request model for health status updates.
/// </summary>
public class HealthUpdateRequest
{
    public HealthStatus Status { get; set; }
    public string? Output { get; set; }
}

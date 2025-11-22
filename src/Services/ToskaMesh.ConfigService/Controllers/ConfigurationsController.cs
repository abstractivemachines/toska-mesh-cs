using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToskaMesh.ConfigService.Models;
using ToskaMesh.ConfigService.Services;

namespace ToskaMesh.ConfigService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationsController : ControllerBase
{
    private readonly IConfigurationService _configurations;

    public ConfigurationsController(IConfigurationService configurations)
    {
        _configurations = configurations;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? environment, CancellationToken cancellationToken)
    {
        var configs = await _configurations.GetAllAsync(environment, cancellationToken);
        return Ok(configs);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, [FromQuery] string environment = "default", CancellationToken cancellationToken = default)
    {
        var config = await _configurations.GetAsync(name, environment, cancellationToken);
        if (config == null)
        {
            return NotFound();
        }

        return Ok(config);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(ConfigurationUpsertRequest request, CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "system";
        var created = await _configurations.UpsertAsync(request, user, cancellationToken);
        return CreatedAtAction(nameof(Get), new { name = created.Name, environment = created.Environment }, created);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, ConfigurationUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Name mismatch");
        }

        var user = User.Identity?.Name ?? "system";
        var updated = await _configurations.UpsertAsync(request, user, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/rollback")]
    public async Task<IActionResult> Rollback(Guid id, RollbackRequest request, CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "system";
        var success = await _configurations.RollbackAsync(id, request, user, cancellationToken);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        var history = await _configurations.GetHistoryAsync(id, cancellationToken);
        return Ok(history);
    }
}

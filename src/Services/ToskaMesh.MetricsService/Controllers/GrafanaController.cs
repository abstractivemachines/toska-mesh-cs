using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToskaMesh.MetricsService.Models;
using ToskaMesh.MetricsService.Services;

namespace ToskaMesh.MetricsService.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class GrafanaController : ControllerBase
{
    private readonly IGrafanaProvisioningService _provisioningService;

    public GrafanaController(IGrafanaProvisioningService provisioningService)
    {
        _provisioningService = provisioningService;
    }

    [HttpGet("dashboards")]
    public async Task<ActionResult<IEnumerable<GrafanaDashboardDto>>> List(CancellationToken cancellationToken)
    {
        var dashboards = await _provisioningService.ListAsync(cancellationToken);
        return Ok(dashboards);
    }

    [HttpPost("dashboards")]
    public async Task<ActionResult<GrafanaDashboardDto>> Create([FromBody] GrafanaDashboardRequest request, CancellationToken cancellationToken)
    {
        var dashboard = await _provisioningService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { dashboard.Id }, dashboard);
    }

    [HttpPut("dashboards/{id:guid}")]
    public async Task<ActionResult<GrafanaDashboardDto>> Update(Guid id, [FromBody] GrafanaDashboardRequest request, CancellationToken cancellationToken)
    {
        var dashboard = await _provisioningService.UpdateAsync(id, request, cancellationToken);
        return Ok(dashboard);
    }

    [HttpDelete("dashboards/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _provisioningService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("provisioning")]
    public async Task<ActionResult<GrafanaProvisioningDocument>> GetProvisioningDocument(CancellationToken cancellationToken)
    {
        var document = await _provisioningService.BuildProvisioningDocumentAsync(cancellationToken);
        return Ok(document);
    }
}

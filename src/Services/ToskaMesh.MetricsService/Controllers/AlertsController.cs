using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToskaMesh.MetricsService.Models;
using ToskaMesh.MetricsService.Services;

namespace ToskaMesh.MetricsService.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRuleService _alertRuleService;

    public AlertsController(IAlertRuleService alertRuleService)
    {
        _alertRuleService = alertRuleService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertRuleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var alerts = await _alertRuleService.ListAsync(cancellationToken);
        return Ok(alerts);
    }

    [HttpPost]
    public async Task<ActionResult<AlertRuleDto>> Create([FromBody] CreateAlertRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _alertRuleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AlertRuleDto>> Update(Guid id, [FromBody] UpdateAlertRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _alertRuleService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _alertRuleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/evaluate")]
    public async Task<ActionResult<AlertEvaluationResult>> Evaluate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _alertRuleService.EvaluateAsync(id, cancellationToken);
        return Ok(result);
    }
}

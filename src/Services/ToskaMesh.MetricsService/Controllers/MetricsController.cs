using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToskaMesh.MetricsService.Entities;
using ToskaMesh.MetricsService.Models;
using ToskaMesh.MetricsService.Services;

namespace ToskaMesh.MetricsService.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsRegistry _metricsRegistry;
    private readonly ICustomMetricService _customMetricService;
    private readonly IMetricHistoryService _metricHistoryService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsRegistry metricsRegistry,
        ICustomMetricService customMetricService,
        IMetricHistoryService metricHistoryService,
        ILogger<MetricsController> logger)
    {
        _metricsRegistry = metricsRegistry;
        _customMetricService = customMetricService;
        _metricHistoryService = metricHistoryService;
        _logger = logger;
    }

    [HttpGet("custom")]
    public async Task<ActionResult<IEnumerable<CustomMetricDefinition>>> ListCustomMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _customMetricService.ListAsync(cancellationToken);
        return Ok(metrics);
    }

    [HttpPost("custom")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomMetricDefinition>> RegisterCustomMetric(
        [FromBody] RegisterCustomMetricRequest request,
        CancellationToken cancellationToken)
    {
        var definition = await _customMetricService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListCustomMetrics), new { definition.Id }, definition);
    }

    [HttpPost("record")]
    public async Task<IActionResult> RecordMetric([FromBody] RecordMetricRequest request, CancellationToken cancellationToken)
    {
        var definition = await _customMetricService.GetRequiredAsync(request.Name, cancellationToken);

        try
        {
            _metricsRegistry.RecordCustomMetric(definition, request.Value, request.Labels);
            await _metricHistoryService.RecordSampleAsync(
                definition.Name,
                request.Value,
                definition.Type,
                request.Labels,
                request.Unit ?? definition.Unit,
                cancellationToken);

            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed recording metric {MetricName}", request.Name);
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("query")]
    public async Task<ActionResult<MetricQueryResponse>> Query([FromQuery] MetricQuery query, CancellationToken cancellationToken)
    {
        var datapoints = await _metricHistoryService.QueryAsync(query, cancellationToken);
        return Ok(new MetricQueryResponse(query.Name, datapoints));
    }
}

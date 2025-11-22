using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToskaMesh.TracingService.Models;
using ToskaMesh.TracingService.Services;

namespace ToskaMesh.TracingService.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TracesController : ControllerBase
{
    private readonly ITraceStorageService _storageService;
    private readonly ITraceAnalyticsService _analyticsService;

    public TracesController(
        ITraceStorageService storageService,
        ITraceAnalyticsService analyticsService)
    {
        _storageService = storageService;
        _analyticsService = analyticsService;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] TraceIngestRequest request, CancellationToken cancellationToken)
    {
        await _storageService.IngestAsync(request, cancellationToken);
        return Accepted();
    }

    [HttpGet]
    public async Task<ActionResult<TraceQueryResponse>> Query([FromQuery] TraceQueryParameters query, CancellationToken cancellationToken)
    {
        var response = await _storageService.QueryAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{traceId}")]
    public async Task<ActionResult<TraceDetailDto>> GetTrace(string traceId, CancellationToken cancellationToken)
    {
        var trace = await _storageService.GetTraceAsync(traceId, cancellationToken);
        if (trace is null)
        {
            return NotFound();
        }

        return Ok(trace);
    }

    [HttpGet("correlation/{correlationId}")]
    public async Task<ActionResult<IEnumerable<TraceSummaryDto>>> ByCorrelation(string correlationId, CancellationToken cancellationToken)
    {
        var traces = await _storageService.GetByCorrelationIdAsync(correlationId, cancellationToken);
        return Ok(traces);
    }

    [HttpGet("performance")]
    public async Task<ActionResult<TracePerformanceResponse>> Performance([FromQuery] TracePerformanceRequest request, CancellationToken cancellationToken)
    {
        var response = await _analyticsService.GetPerformanceAsync(request, cancellationToken);
        return Ok(response);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToskaMesh.Gateway.Models;
using ToskaMesh.Gateway.Services;

namespace ToskaMesh.Gateway.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardProxyService _proxyService;

    public DashboardController(IDashboardProxyService proxyService)
    {
        _proxyService = proxyService;
    }

    [HttpGet("prometheus/query")]
    public async Task<IActionResult> QueryPrometheus(CancellationToken cancellationToken)
    {
        return await ProxyPrometheusAsync("api/v1/query", cancellationToken);
    }

    [HttpGet("prometheus/query-range")]
    public async Task<IActionResult> QueryPrometheusRange(CancellationToken cancellationToken)
    {
        return await ProxyPrometheusAsync("api/v1/query_range", cancellationToken);
    }

    [HttpGet("prometheus/labels")]
    public async Task<IActionResult> ListPrometheusLabels(CancellationToken cancellationToken)
    {
        return await ProxyPrometheusAsync("api/v1/labels", cancellationToken);
    }

    [HttpGet("prometheus/series")]
    public async Task<IActionResult> ListPrometheusSeries(CancellationToken cancellationToken)
    {
        return await ProxyPrometheusAsync("api/v1/series", cancellationToken);
    }

    [HttpGet("traces")]
    public async Task<IActionResult> QueryTraces(CancellationToken cancellationToken)
    {
        return await ProxyTracingAsync("api/traces", cancellationToken);
    }

    [HttpGet("traces/{traceId}")]
    public async Task<IActionResult> GetTrace(string traceId, CancellationToken cancellationToken)
    {
        return await ProxyTracingAsync($"api/traces/{traceId}", cancellationToken);
    }

    [HttpGet("traces/correlation/{correlationId}")]
    public async Task<IActionResult> GetTracesByCorrelation(string correlationId, CancellationToken cancellationToken)
    {
        return await ProxyTracingAsync($"api/traces/correlation/{correlationId}", cancellationToken);
    }

    [HttpGet("traces/performance")]
    public async Task<IActionResult> GetTracePerformance(CancellationToken cancellationToken)
    {
        return await ProxyTracingAsync("api/traces/performance", cancellationToken);
    }

    [HttpGet("services")]
    public async Task<ActionResult<IReadOnlyCollection<DashboardServiceCatalogItem>>> GetServiceCatalog(
        CancellationToken cancellationToken)
    {
        var catalog = await _proxyService.GetServiceCatalogAsync(cancellationToken);
        return Ok(catalog);
    }

    private async Task<IActionResult> ProxyPrometheusAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _proxyService.ProxyPrometheusAsync(AppendQuery(path), cancellationToken);
        return result == null
            ? StatusCode(StatusCodes.Status502BadGateway)
            : new ContentResult
            {
                Content = result.Content,
                ContentType = result.ContentType,
                StatusCode = result.StatusCode
            };
    }

    private async Task<IActionResult> ProxyTracingAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _proxyService.ProxyTracingAsync(AppendQuery(path), cancellationToken);
        return result == null
            ? StatusCode(StatusCodes.Status502BadGateway)
            : new ContentResult
            {
                Content = result.Content,
                ContentType = result.ContentType,
                StatusCode = result.StatusCode
            };
    }

    private string AppendQuery(string path)
    {
        return Request.QueryString.HasValue
            ? $"{path}{Request.QueryString.Value}"
            : path;
    }
}

using Microsoft.AspNetCore.Mvc;
using ToskaMesh.HealthMonitor.Services;

namespace ToskaMesh.HealthMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly HealthReportCache _cache;

    public StatusController(HealthReportCache cache)
    {
        _cache = cache;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_cache.GetAll());
    }

    [HttpGet("{serviceName}")]
    public IActionResult Get(string serviceName)
    {
        return Ok(_cache.GetByService(serviceName));
    }
}

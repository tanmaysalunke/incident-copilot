using Microsoft.AspNetCore.Mvc;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        _logger.LogInformation("Health check requested");

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "IncidentCopilot",
            version = "1.0.0",
            endpoints = new
            {
                health = "GET /api/health",
                ingest = "POST /api/incidents/ingest",
                investigate = "POST /api/incidents/investigate",
                search = "POST /api/search",
                compare = "POST /api/search/compare",
                seed = "POST /api/seed",
                services = "GET /api/services/graph",
                incidents = "GET /api/incidents"
            }
        });
    }
}
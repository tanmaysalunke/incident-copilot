using Microsoft.AspNetCore.Mvc;

namespace IncidentCopilot.Controllers;

// [ApiController] tells ASP.NET this class handles HTTP requests
// [Route("api/[controller]")] maps this to /api/health
// In Python/FastAPI, this is like @app.get("/api/health")
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    // Constructor injection. ASP.NET automatically provides the logger.
    // In Python, this is like __init__(self, logger).
    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    // GET /api/health
    [HttpGet]
    public IActionResult GetHealth()
    {
        _logger.LogInformation("Health check requested");

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "IncidentCopilot",
            version = "1.0.0"
        });
    }
}
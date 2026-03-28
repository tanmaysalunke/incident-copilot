using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(ILogger<ServicesController> logger)
    {
        _logger = logger;
    }

    // GET /api/services/graph - Will be implemented on Day 5
    [HttpGet("graph")]
    public IActionResult GetServiceGraph()
    {
        _logger.LogInformation("Service graph requested");

        // Placeholder
        return Ok(ApiResponse<string>.Ok("Service graph not yet implemented."));
    }
}
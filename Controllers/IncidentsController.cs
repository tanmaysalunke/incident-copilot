using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly ILogger<IncidentsController> _logger;

    public IncidentsController(ILogger<IncidentsController> logger)
    {
        _logger = logger;
    }

    // POST /api/incidents/ingest - Will be implemented on Day 3
    [HttpPost("ingest")]
    public IActionResult Ingest([FromBody] IngestRequest request)
    {
        _logger.LogInformation(
            "Ingest request received for service {ServiceName} with {EntryCount} entries",
            request.ServiceName,
            request.Entries.Count
        );

        // Placeholder: will be replaced with actual ingestion logic on Day 3
        return Ok(ApiResponse<string>.Ok($"Received {request.Entries.Count} entries for {request.ServiceName}"));
    }

    // POST /api/incidents/investigate - Will be implemented on Day 6
    [HttpPost("investigate")]
    public IActionResult Investigate([FromBody] InvestigateRequest request)
    {
        _logger.LogInformation("Investigation query: {Question}", request.Question);

        // Placeholder: will be replaced with AI investigation on Day 6
        return Ok(ApiResponse<InvestigateResponse>.Ok(new InvestigateResponse
        {
            Answer = "Investigation engine not yet implemented. This will be built on Day 6.",
            SessionId = request.SessionId ?? Guid.NewGuid().ToString()
        }));
    }

    // GET /api/incidents/{id}/timeline - Will be implemented on Day 5
    [HttpGet("{id}/timeline")]
    public IActionResult GetTimeline(string id)
    {
        _logger.LogInformation("Timeline requested for incident {IncidentId}", id);

        // Placeholder
        return Ok(ApiResponse<string>.Ok($"Timeline for incident {id} not yet implemented."));
    }
}
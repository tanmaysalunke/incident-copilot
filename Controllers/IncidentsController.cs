using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly ILogger<IncidentsController> _logger;
    private readonly CosmosLogRepository? _logRepo;
    private readonly CosmosIncidentRepository? _incidentRepo;

    // The ? means these can be null (if Cosmos DB is not configured)
    public IncidentsController(
        ILogger<IncidentsController> logger,
        CosmosLogRepository? logRepo = null,
        CosmosIncidentRepository? incidentRepo = null)
    {
        _logger = logger;
        _logRepo = logRepo;
        _incidentRepo = incidentRepo;
    }

    // POST /api/incidents/ingest - Stub for now, Day 3 will add real ingestion
    [HttpPost("ingest")]
    public IActionResult Ingest([FromBody] IngestRequest request)
    {
        _logger.LogInformation(
            "Ingest request received for service {ServiceName} with {EntryCount} entries",
            request.ServiceName,
            request.Entries.Count
        );

        return Ok(ApiResponse<string>.Ok(
            $"Received {request.Entries.Count} entries for {request.ServiceName}"));
    }

    // POST /api/incidents/investigate - Stub for now, Day 6 will add AI
    [HttpPost("investigate")]
    public IActionResult Investigate([FromBody] InvestigateRequest request)
    {
        _logger.LogInformation("Investigation query: {Question}", request.Question);

        return Ok(ApiResponse<InvestigateResponse>.Ok(new InvestigateResponse
        {
            Answer = "Investigation engine not yet implemented. Coming on Day 6.",
            SessionId = request.SessionId ?? Guid.NewGuid().ToString()
        }));
    }

    // POST /api/incidents - Create a new incident record
    [HttpPost]
    public async Task<IActionResult> CreateIncident([FromBody] Incident incident)
    {
        if (_incidentRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        var created = await _incidentRepo.CreateAsync(incident);
        _logger.LogInformation("Created incident: {Title}", created.Title);

        return Ok(ApiResponse<Incident>.Ok(created));
    }

    // GET /api/incidents - List all incidents
    [HttpGet]
    public async Task<IActionResult> GetAllIncidents()
    {
        if (_incidentRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        var incidents = await _incidentRepo.GetAllAsync();
        return Ok(ApiResponse<List<Incident>>.Ok(incidents));
    }

    // GET /api/incidents/{id}/timeline - Get incident timeline
    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> GetTimeline(string id)
    {
        if (_incidentRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        var incident = await _incidentRepo.GetByIdAsync(id);
        if (incident == null)
            return NotFound(ApiResponse<string>.Fail($"Incident {id} not found"));

        return Ok(ApiResponse<Incident>.Ok(incident));
    }
}
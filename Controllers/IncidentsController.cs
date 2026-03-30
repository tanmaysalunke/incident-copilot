using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;
using IncidentCopilot.Services;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly ILogger<IncidentsController> _logger;
    private readonly CosmosLogRepository? _logRepo;
    private readonly CosmosIncidentRepository? _incidentRepo;
    private readonly IngestionService? _ingestionService;
    private readonly InvestigationService? _investigationService;

    // The ? means these can be null (if Cosmos DB is not configured)
    public IncidentsController(
        ILogger<IncidentsController> logger,
        CosmosLogRepository? logRepo = null,
        CosmosIncidentRepository? incidentRepo = null,
        IngestionService? ingestionService = null,
        InvestigationService? investigationService = null)
    {
        _logger = logger;
        _logRepo = logRepo;
        _incidentRepo = incidentRepo;
        _ingestionService = ingestionService;
        _investigationService = investigationService;
    }

    // POST /api/incidents/ingest - Ingest log entries through the pipeline
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestRequest request)
    {
        if (_ingestionService == null)
            return StatusCode(503, ApiResponse<string>.Fail("Ingestion service not configured"));

        _logger.LogInformation(
            "Ingest request received for service {ServiceName} with {EntryCount} entries",
            request.ServiceName, request.Entries.Count
        );

        var result = await _ingestionService.IngestAsync(request.ServiceName, request.Entries);

        return Ok(ApiResponse<IngestionResult>.Ok(result));
    }

    // POST /api/incidents/investigate - AI-powered investigation
    [HttpPost("investigate")]
    public async Task<IActionResult> Investigate([FromBody] InvestigateRequest request)
    {
        if (_investigationService == null)
            return StatusCode(503, ApiResponse<string>.Fail("Investigation service not configured"));

        _logger.LogInformation("Investigation query: {Question}", request.Question);

        var response = await _investigationService.InvestigateAsync(
            request.Question,
            request.SessionId
        );

        return Ok(ApiResponse<InvestigationResponse>.Ok(response));
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
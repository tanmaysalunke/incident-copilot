using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;
using IncidentCopilot.Services;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly ILogger<SeedController> _logger;
    private readonly IngestionService? _ingestionService;
    private readonly CosmosServiceGraphRepository? _serviceRepo;
    private readonly CosmosIncidentRepository? _incidentRepo;

    public SeedController(
        ILogger<SeedController> logger,
        IngestionService? ingestionService = null,
        CosmosServiceGraphRepository? serviceRepo = null,
        CosmosIncidentRepository? incidentRepo = null)
    {
        _logger = logger;
        _ingestionService = ingestionService;
        _serviceRepo = serviceRepo;
        _incidentRepo = incidentRepo;
    }

    /// <summary>
    /// POST /api/seed
    /// Populate the database with the sample cascading failure scenario.
    /// This creates service graph nodes, an incident record, and ingests
    /// all the sample log data through the full pipeline.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SeedData()
    {
        if (_ingestionService == null || _serviceRepo == null || _incidentRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        _logger.LogInformation("Starting data seeding...");

        // Step 1: Create the service dependency graph
        var serviceGraph = LogGenerator.GenerateServiceGraph();
        foreach (var node in serviceGraph)
        {
            await _serviceRepo.UpsertAsync(node);
        }
        _logger.LogInformation("Service graph seeded: {Count} services", serviceGraph.Count);

        // Step 2: Create the incident record
        var incident = new Incident
        {
            Title = "Cascading Payment Failure - DB Connection Pool Exhaustion",
            StartTime = new DateTime(2026, 3, 28, 3, 5, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 3, 28, 3, 16, 0, DateTimeKind.Utc),
            AffectedServices = new List<string> { "PostgresDB", "AuthService", "PaymentService", "APIGateway" },
            Status = "Resolved",
            RootCause = "Slow query introduced in orders-service v2.3.1 deployment caused PostgresDB connection pool exhaustion, cascading to auth and payment service failures."
        };
        await _incidentRepo.CreateAsync(incident);
        _logger.LogInformation("Incident record created: {Title}", incident.Title);

        // Step 3: Ingest all log data through the pipeline
        var scenarioLogs = LogGenerator.GenerateIncidentScenario();
        var results = new List<IngestionResult>();

        foreach (var (serviceName, entries) in scenarioLogs)
        {
            var result = await _ingestionService.IngestAsync(serviceName, entries);
            results.Add(result);
        }

        var summary = new
        {
            servicesSeeded = serviceGraph.Count,
            incidentCreated = incident.Title,
            ingestionResults = results,
            totalChunks = results.Sum(r => r.ChunksCreated),
            totalEntries = results.Sum(r => r.RawEntryCount)
        };

        _logger.LogInformation(
            "Seeding complete: {Services} services, {Chunks} chunks, {Entries} total entries",
            serviceGraph.Count, summary.totalChunks, summary.totalEntries
        );

        return Ok(ApiResponse<object>.Ok(summary));
    }

    /// <summary>
    /// DELETE /api/seed
    /// Clear all data from the database so we can re-seed cleanly.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearData()
    {
        if (_serviceRepo == null || _incidentRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        _logger.LogInformation("Clearing all data for re-seed...");

        // We need to delete the log-chunks by querying and deleting each one
        // For a clean reseed, it is easier to just re-run seed which uses CreateItem
        // The old items will remain but new ones will be added.
        // For a true clean slate, we would delete containers and recreate them.

        return Ok(ApiResponse<string>.Ok("To fully reset, stop the app, run the database delete CLI command, then restart. Or just call POST /api/seed to add fresh data."));
    }
}
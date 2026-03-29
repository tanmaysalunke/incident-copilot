using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ILogger<ServicesController> _logger;
    private readonly CosmosServiceGraphRepository? _serviceRepo;

    public ServicesController(
        ILogger<ServicesController> logger,
        CosmosServiceGraphRepository? serviceRepo = null)
    {
        _logger = logger;
        _serviceRepo = serviceRepo;
    }

    // GET /api/services/graph - Get the full service dependency graph
    [HttpGet("graph")]
    public async Task<IActionResult> GetServiceGraph()
    {
        if (_serviceRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        var services = await _serviceRepo.GetAllAsync();
        return Ok(ApiResponse<List<ServiceNode>>.Ok(services));
    }

    // POST /api/services - Add or update a service in the graph
    [HttpPost]
    public async Task<IActionResult> UpsertService([FromBody] ServiceNode node)
    {
        if (_serviceRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Database not configured"));

        var result = await _serviceRepo.UpsertAsync(node);
        _logger.LogInformation("Upserted service: {ServiceName}", result.Id);

        return Ok(ApiResponse<ServiceNode>.Ok(result));
    }
}
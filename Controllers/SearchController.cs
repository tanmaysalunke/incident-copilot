using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;
using IncidentCopilot.Services;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly EmbeddingService? _embeddingService;
    private readonly CosmosLogRepository? _logRepo;

    public SearchController(
        ILogger<SearchController> logger,
        EmbeddingService? embeddingService = null,
        CosmosLogRepository? logRepo = null)
    {
        _logger = logger;
        _embeddingService = embeddingService;
        _logRepo = logRepo;
    }

    /// <summary>
    /// POST /api/search
    /// Search log chunks using natural language.
    /// The query is converted to a vector and compared against stored log chunk embeddings.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        if (_embeddingService == null || _logRepo == null)
            return StatusCode(503, ApiResponse<string>.Fail("Search services not configured"));

        _logger.LogInformation("Search query: {Query}", request.Query);

        // Step 1: Convert the user's question into a vector
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query);

        // Step 2: Find the most similar log chunks in Cosmos DB
        var results = await _logRepo.VectorSearchAsync(
            queryEmbedding,
            serviceName: request.ServiceName,
            topK: request.TopK
        );

        _logger.LogInformation(
            "Search returned {Count} results for query: {Query}",
            results.Count, request.Query
        );

        // Step 3: Return results without the embedding vectors (they are huge and not useful to display)
        var response = results.Select(r => new SearchResultItem
        {
            Id = r.Id,
            ServiceName = r.ServiceName,
            TimeStart = r.TimeStart,
            TimeEnd = r.TimeEnd,
            Severity = r.Severity,
            ChunkText = r.ChunkText,
            EntryCount = r.RawEntries.Count
        }).ToList();

        return Ok(ApiResponse<List<SearchResultItem>>.Ok(response));
    }
}

/// <summary>
/// What the client sends when searching.
/// </summary>
public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? ServiceName { get; set; } // Optional: filter by service
    public int TopK { get; set; } = 5; // Number of results to return
}

/// <summary>
/// A single search result (log chunk without the embedding vector).
/// </summary>
public class SearchResultItem
{
    public string Id { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime TimeStart { get; set; }
    public DateTime TimeEnd { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public int EntryCount { get; set; }
}
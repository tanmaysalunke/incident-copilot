using Microsoft.AspNetCore.Mvc;
using IncidentCopilot.Models;
using IncidentCopilot.Services;

namespace IncidentCopilot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly RetrievalService? _retrievalService;

    public SearchController(
        ILogger<SearchController> logger,
        RetrievalService? retrievalService = null)
    {
        _logger = logger;
        _retrievalService = retrievalService;
    }

    /// <summary>
    /// POST /api/search
    /// Hybrid search: temporal filter + vector search + severity boost + cross-service correlation.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HybridSearch([FromBody] SearchRequest request)
    {
        if (_retrievalService == null)
            return StatusCode(503, ApiResponse<string>.Fail("Retrieval service not configured"));

        var query = new RetrievalQuery
        {
            Question = request.Query,
            ServiceName = request.ServiceName,
            TimeStart = request.TimeStart,
            TimeEnd = request.TimeEnd,
            TopK = request.TopK
        };

        var result = await _retrievalService.RetrieveAsync(query);

        var response = FormatResult(result);
        return Ok(ApiResponse<object>.Ok(response));
    }

    /// <summary>
    /// POST /api/search/compare
    /// Compare hybrid retrieval vs naive vector search side by side.
    /// This endpoint is great for demos and interviews.
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareSearch([FromBody] SearchRequest request)
    {
        if (_retrievalService == null)
            return StatusCode(503, ApiResponse<string>.Fail("Retrieval service not configured"));

        // Run hybrid retrieval
        var hybridQuery = new RetrievalQuery
        {
            Question = request.Query,
            ServiceName = request.ServiceName,
            TimeStart = request.TimeStart,
            TimeEnd = request.TimeEnd,
            TopK = request.TopK
        };
        var hybridResult = await _retrievalService.RetrieveAsync(hybridQuery);

        // Run naive vector search
        var naiveResult = await _retrievalService.NaiveSearchAsync(request.Query, request.TopK);

        var comparison = new
        {
            query = request.Query,
            hybrid = FormatResult(hybridResult),
            naive = FormatResult(naiveResult),
            analysis = GenerateComparison(hybridResult, naiveResult)
        };

        return Ok(ApiResponse<object>.Ok(comparison));
    }

    private object FormatResult(RetrievalResult result)
    {
        return new
        {
            servicesSearched = result.ServicesSearched,
            totalCandidates = result.TotalCandidates,
            results = result.Results.Select(r => new
            {
                serviceName = r.Chunk.ServiceName,
                timeStart = r.Chunk.TimeStart,
                timeEnd = r.Chunk.TimeEnd,
                severity = r.Chunk.Severity,
                similarityScore = Math.Round(r.SimilarityScore, 4),
                severityWeight = r.SeverityWeight,
                finalScore = Math.Round(r.FinalScore, 4),
                fromRelatedService = r.IsFromRelatedService,
                chunkText = r.Chunk.ChunkText,
                entryCount = r.Chunk.RawEntries.Count
            }).ToList()
        };
    }

    private object GenerateComparison(RetrievalResult hybrid, RetrievalResult naive)
    {
        // Count how many ERROR/FATAL chunks each method surfaced
        var hybridErrors = hybrid.Results.Count(r =>
            r.Chunk.Severity == "ERROR" || r.Chunk.Severity == "FATAL");
        var naiveErrors = naive.Results.Count(r =>
            r.Chunk.Severity == "ERROR" || r.Chunk.Severity == "FATAL");

        // Count unique services found
        var hybridServices = hybrid.Results.Select(r => r.Chunk.ServiceName).Distinct().Count();
        var naiveServices = naive.Results.Select(r => r.Chunk.ServiceName).Distinct().Count();

        // Count cross-service results in hybrid
        var crossServiceResults = hybrid.Results.Count(r => r.IsFromRelatedService);

        return new
        {
            hybridErrorChunks = hybridErrors,
            naiveErrorChunks = naiveErrors,
            hybridServicesFound = hybridServices,
            naiveServicesFound = naiveServices,
            crossServiceExpansionResults = crossServiceResults,
            summary = $"Hybrid found {hybridErrors} error/fatal chunks across {hybridServices} services " +
                      $"(including {crossServiceResults} from related services). " +
                      $"Naive found {naiveErrors} error/fatal chunks across {naiveServices} services."
        };
    }
}

/// <summary>
/// Search request with optional temporal filtering.
/// </summary>
public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public DateTime? TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public int TopK { get; set; } = 5;
}
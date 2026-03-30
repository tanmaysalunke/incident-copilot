using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Services;

/// <summary>
/// Hybrid retrieval engine that combines temporal filtering, vector search,
/// and severity boosting for incident investigation.
///
/// Why not just use vector search?
///
/// Pure vector search has a critical flaw for log data: it finds logs that
/// are semantically similar but may be from the wrong time period. If your
/// database has logs from 100 different incidents, a query about "payment
/// failure" might return logs from last month's payment incident instead
/// of tonight's. Temporal filtering solves this.
///
/// Severity boosting addresses another problem: during an incident, there
/// are thousands of INFO/DEBUG logs but only a handful of ERROR/FATAL logs.
/// The errors are what matter for root cause analysis, so we boost them
/// in the ranking.
///
/// Cross-service correlation mimics how experienced SREs investigate:
/// they don't just look at the failing service, they trace the dependency
/// chain to find the root cause upstream.
/// </summary>
public class RetrievalService
{
    private readonly EmbeddingService _embeddingService;
    private readonly CosmosLogRepository _logRepo;
    private readonly CosmosServiceGraphRepository _serviceRepo;
    private readonly ILogger<RetrievalService> _logger;

    // Severity weights: higher weight = more important in ranking
    private static readonly Dictionary<string, double> SeverityWeights = new()
    {
        ["DEBUG"] = 0.5,
        ["INFO"] = 0.7,
        ["WARN"] = 1.0,
        ["ERROR"] = 1.5,
        ["FATAL"] = 2.0
    };

    public RetrievalService(
        EmbeddingService embeddingService,
        CosmosLogRepository logRepo,
        CosmosServiceGraphRepository serviceRepo,
        ILogger<RetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _logRepo = logRepo;
        _serviceRepo = serviceRepo;
        _logger = logger;
    }

    /// <summary>
    /// Hybrid retrieval: the main entry point for finding relevant log chunks.
    ///
    /// Three stages:
    /// 1. Temporal filter: narrow to the relevant time window
    /// 2. Vector search: find semantically similar chunks within that window
    /// 3. Severity boost: re-rank results to prioritize ERROR/FATAL
    ///
    /// Plus cross-service expansion using the dependency graph.
    /// </summary>
    public async Task<RetrievalResult> RetrieveAsync(RetrievalQuery query)
    {
        _logger.LogInformation(
            "Hybrid retrieval: query={Query}, timeStart={Start}, timeEnd={End}, service={Service}",
            query.Question, query.TimeStart, query.TimeEnd, query.ServiceName
        );

        // Generate embedding for the user's question
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query.Question);

        // Determine which services to search
        var servicesToSearch = await GetServicesToSearch(query.ServiceName);

        _logger.LogInformation(
            "Searching across {Count} services: {Services}",
            servicesToSearch.Count, string.Join(", ", servicesToSearch)
        );

        // Collect results from all services
        var allChunks = new List<ScoredChunk>();

        foreach (var service in servicesToSearch)
        {
            List<LogChunk> chunks;

            if (query.TimeStart.HasValue && query.TimeEnd.HasValue)
            {
                // Stage 1: Temporal filter - get chunks within time window
                chunks = await _logRepo.GetByTimeRangeAsync(
                    service, query.TimeStart.Value, query.TimeEnd.Value
                );

                _logger.LogInformation(
                    "Temporal filter for {Service}: {Count} chunks in window",
                    service, chunks.Count
                );
            }
            else
            {
                // No time filter: get all chunks for this service
                chunks = await _logRepo.GetByServiceAsync(service);
            }

            // Stage 2: Vector search - score each chunk by semantic similarity
            foreach (var chunk in chunks)
            {
                if (chunk.Embedding == null) continue;

                var similarityScore = CosineSimilarity(queryEmbedding, chunk.Embedding);

                // Stage 3: Severity boost - multiply similarity by severity weight
                var severityWeight = SeverityWeights.GetValueOrDefault(chunk.Severity, 1.0);
                var boostedScore = similarityScore * severityWeight;

                allChunks.Add(new ScoredChunk
                {
                    Chunk = chunk,
                    SimilarityScore = similarityScore,
                    SeverityWeight = severityWeight,
                    FinalScore = boostedScore,
                    IsFromRelatedService = service != query.ServiceName
                });
            }
        }

        // Sort by final score (highest first) and take top results
        var topChunks = allChunks
            .OrderByDescending(c => c.FinalScore)
            .Take(query.TopK)
            .ToList();

        _logger.LogInformation(
            "Hybrid retrieval complete: {Total} candidates -> {TopK} results",
            allChunks.Count, topChunks.Count
        );

        return new RetrievalResult
        {
            Query = query.Question,
            Results = topChunks,
            ServicesSearched = servicesToSearch,
            TotalCandidates = allChunks.Count
        };
    }

    /// <summary>
    /// Naive vector search (no temporal filtering, no severity boosting).
    /// Used for comparison to show why hybrid retrieval is better.
    /// </summary>
    public async Task<RetrievalResult> NaiveSearchAsync(string question, int topK = 5)
    {
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);

        // Just do a raw vector search across all services
        var chunks = await _logRepo.VectorSearchAsync(queryEmbedding, topK: topK);

        var scoredChunks = chunks.Select(c => new ScoredChunk
        {
            Chunk = c,
            SimilarityScore = c.Embedding != null
                ? CosineSimilarity(queryEmbedding, c.Embedding)
                : 0,
            SeverityWeight = 1.0, // No boosting
            FinalScore = c.Embedding != null
                ? CosineSimilarity(queryEmbedding, c.Embedding)
                : 0,
            IsFromRelatedService = false
        }).ToList();

        return new RetrievalResult
        {
            Query = question,
            Results = scoredChunks,
            ServicesSearched = new List<string> { "all (no filter)" },
            TotalCandidates = chunks.Count
        };
    }

    /// <summary>
    /// Determine which services to search based on the dependency graph.
    /// If a specific service is mentioned, also search its upstream and
    /// downstream dependencies.
    /// </summary>
    private async Task<List<string>> GetServicesToSearch(string? serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            // No service specified: search all services
            var allServices = await _serviceRepo.GetAllAsync();
            return allServices.Select(s => s.Id).ToList();
        }

        // Get the specified service and its dependencies
        var services = new HashSet<string> { serviceName };

        var serviceNode = await _serviceRepo.GetByIdAsync(serviceName);
        if (serviceNode != null)
        {
            // Add upstream services (who calls this service)
            foreach (var upstream in serviceNode.UpstreamServices)
                services.Add(upstream);

            // Add downstream services (who this service calls)
            foreach (var downstream in serviceNode.DownstreamServices)
                services.Add(downstream);
        }

        return services.ToList();
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors.
    /// Returns a value between -1 (opposite) and 1 (identical).
    /// Higher = more similar.
    ///
    /// This is the same formula used by Cosmos DB's VectorDistance function,
    /// but we use it here for re-ranking after temporal filtering.
    ///
    /// In Python, this would be:
    ///   np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b))
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denominator == 0) return 0;

        return dotProduct / denominator;
    }
}

/// <summary>
/// Input for a hybrid retrieval query.
/// </summary>
public class RetrievalQuery
{
    public string Question { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public DateTime? TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public int TopK { get; set; } = 5;
}

/// <summary>
/// A log chunk with its retrieval scores.
/// </summary>
public class ScoredChunk
{
    public LogChunk Chunk { get; set; } = new();
    public double SimilarityScore { get; set; }  // Raw vector similarity
    public double SeverityWeight { get; set; }    // Multiplier based on severity
    public double FinalScore { get; set; }        // SimilarityScore * SeverityWeight
    public bool IsFromRelatedService { get; set; } // Found via dependency graph expansion
}

/// <summary>
/// Complete retrieval results with metadata.
/// </summary>
public class RetrievalResult
{
    public string Query { get; set; } = string.Empty;
    public List<ScoredChunk> Results { get; set; } = new();
    public List<string> ServicesSearched { get; set; } = new();
    public int TotalCandidates { get; set; }
}
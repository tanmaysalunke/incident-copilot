namespace IncidentCopilot.Services;

using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

/// <summary>
/// Orchestrates the log ingestion pipeline:
/// 1. Normalize raw log entries
/// 2. Group them into temporal chunks
/// 3. Generate embeddings for each chunk (NEW on Day 4)
/// 4. Store the chunks with embeddings in Cosmos DB
/// </summary>
public class IngestionService
{
    private readonly LogNormalizer _normalizer;
    private readonly TemporalChunker _chunker;
    private readonly EmbeddingService _embeddingService;
    private readonly CosmosLogRepository _logRepo;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        LogNormalizer normalizer,
        TemporalChunker chunker,
        EmbeddingService embeddingService,
        CosmosLogRepository logRepo,
        ILogger<IngestionService> logger)
    {
        _normalizer = normalizer;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _logRepo = logRepo;
        _logger = logger;
    }

    /// <summary>
    /// Ingest a batch of log entries for a service.
    /// Now includes embedding generation for each chunk.
    /// </summary>
    public async Task<IngestionResult> IngestAsync(string serviceName, List<LogEntry> rawEntries)
    {
        _logger.LogInformation(
            "Starting ingestion for {Service}: {Count} raw entries",
            serviceName, rawEntries.Count
        );

        // Step 1: Normalize
        var normalized = _normalizer.Normalize(serviceName, rawEntries);

        // Step 2: Chunk by time window
        var chunks = _chunker.ChunkByTimeWindow(serviceName, normalized);

        // Step 3: Generate embeddings for each chunk
        if (chunks.Count > 0)
        {
            _logger.LogInformation("Generating embeddings for {Count} chunks...", chunks.Count);

            var chunkTexts = chunks.Select(c => c.ChunkText).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingBatchAsync(chunkTexts);

            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = embeddings[i];
            }

            _logger.LogInformation("Embeddings generated for all {Count} chunks", chunks.Count);
        }

        // Step 4: Store in Cosmos DB
        var storedCount = await _logRepo.BulkCreateAsync(chunks);

        _logger.LogInformation(
            "Ingestion complete for {Service}: {Entries} entries -> {Chunks} chunks stored with embeddings",
            serviceName, rawEntries.Count, storedCount
        );

        return new IngestionResult
        {
            ServiceName = serviceName,
            RawEntryCount = rawEntries.Count,
            ChunksCreated = storedCount,
            TimeRange = chunks.Count > 0
                ? $"{chunks.First().TimeStart:HH:mm} - {chunks.Last().TimeEnd:HH:mm}"
                : "N/A"
        };
    }
}

/// <summary>
/// Summary of what happened during ingestion.
/// </summary>
public class IngestionResult
{
    public string ServiceName { get; set; } = string.Empty;
    public int RawEntryCount { get; set; }
    public int ChunksCreated { get; set; }
    public string TimeRange { get; set; } = string.Empty;
}
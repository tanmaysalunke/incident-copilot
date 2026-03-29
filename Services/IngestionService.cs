namespace IncidentCopilot.Services;

using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

/// <summary>
/// Orchestrates the log ingestion pipeline:
/// 1. Normalize raw log entries
/// 2. Group them into temporal chunks
/// 3. Store the chunks in Cosmos DB
///
/// Think of this as the "controller" of the data pipeline.
/// The actual work is done by LogNormalizer and TemporalChunker.
/// </summary>
public class IngestionService
{
    private readonly LogNormalizer _normalizer;
    private readonly TemporalChunker _chunker;
    private readonly CosmosLogRepository _logRepo;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        LogNormalizer normalizer,
        TemporalChunker chunker,
        CosmosLogRepository logRepo,
        ILogger<IngestionService> logger)
    {
        _normalizer = normalizer;
        _chunker = chunker;
        _logRepo = logRepo;
        _logger = logger;
    }

    /// <summary>
    /// Ingest a batch of log entries for a service.
    /// Returns the number of chunks created.
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

        // Step 3: Store in Cosmos DB
        var storedCount = await _logRepo.BulkCreateAsync(chunks);

        _logger.LogInformation(
            "Ingestion complete for {Service}: {Entries} entries -> {Chunks} chunks stored",
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
/// Returned to the caller so they know what was processed.
/// </summary>
public class IngestionResult
{
    public string ServiceName { get; set; } = string.Empty;
    public int RawEntryCount { get; set; }
    public int ChunksCreated { get; set; }
    public string TimeRange { get; set; } = string.Empty;
}
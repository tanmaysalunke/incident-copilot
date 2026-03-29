namespace IncidentCopilot.Services;

using IncidentCopilot.Models;

/// <summary>
/// Groups log entries into temporal chunks (time windows).
///
/// Why temporal chunking instead of fixed-size text chunking?
///
/// Fixed-size chunking (e.g., 500 tokens per chunk) is the default in most
/// RAG tutorials, but it is terrible for log data because:
/// 1. It splits related events across chunks (a cascade that spans 30 seconds
///    might end up in two different chunks)
/// 2. It loses temporal context (which event came first?)
/// 3. It mixes log entries from different time periods
///
/// Temporal chunking solves all three problems by grouping logs into
/// time-based windows (default: 5 minutes). All logs from one service
/// within a 5-minute window become a single chunk, preserving the
/// chronological sequence of events.
/// </summary>
public class TemporalChunker
{
    private readonly ILogger<TemporalChunker> _logger;
    private readonly LogNormalizer _normalizer;

    // Default window size in minutes
    private const int DefaultWindowMinutes = 5;

    public TemporalChunker(ILogger<TemporalChunker> logger, LogNormalizer normalizer)
    {
        _logger = logger;
        _normalizer = normalizer;
    }

    /// <summary>
    /// Takes a flat list of log entries and groups them into temporal chunks.
    /// Each chunk contains all entries from one service within one time window.
    /// </summary>
    public List<LogChunk> ChunkByTimeWindow(
        string serviceName,
        List<LogEntry> entries,
        int windowMinutes = DefaultWindowMinutes)
    {
        if (entries.Count == 0)
            return new List<LogChunk>();

        // Sort entries by timestamp (oldest first)
        var sorted = entries.OrderBy(e => e.Timestamp).ToList();

        // Find the start of the first window
        var windowStart = GetWindowStart(sorted[0].Timestamp, windowMinutes);

        var chunks = new List<LogChunk>();
        var currentWindowEntries = new List<LogEntry>();
        var currentWindowStart = windowStart;

        foreach (var entry in sorted)
        {
            var entryWindowStart = GetWindowStart(entry.Timestamp, windowMinutes);

            // If this entry belongs to a new window, save the current chunk and start a new one
            if (entryWindowStart != currentWindowStart && currentWindowEntries.Count > 0)
            {
                chunks.Add(CreateChunk(serviceName, currentWindowStart, windowMinutes, currentWindowEntries));
                currentWindowEntries = new List<LogEntry>();
                currentWindowStart = entryWindowStart;
            }

            currentWindowEntries.Add(entry);
        }

        // Don't forget the last window
        if (currentWindowEntries.Count > 0)
        {
            chunks.Add(CreateChunk(serviceName, currentWindowStart, windowMinutes, currentWindowEntries));
        }

        _logger.LogInformation(
            "Chunked {EntryCount} entries into {ChunkCount} temporal chunks for {Service} (window: {Window}min)",
            entries.Count, chunks.Count, serviceName, windowMinutes
        );

        return chunks;
    }

    /// <summary>
    /// Create a LogChunk from a group of entries in the same time window.
    /// </summary>
    private LogChunk CreateChunk(
        string serviceName,
        DateTime windowStart,
        int windowMinutes,
        List<LogEntry> entries)
    {
        var windowEnd = windowStart.AddMinutes(windowMinutes);

        // Build the chunk text: a concatenation of all log messages in this window.
        // This text will be embedded (converted to a vector) on Day 4.
        var chunkText = BuildChunkText(serviceName, windowStart, windowEnd, entries);

        return new LogChunk
        {
            Id = Guid.NewGuid().ToString(),
            ServiceName = serviceName,
            TimeStart = windowStart,
            TimeEnd = windowEnd,
            Severity = _normalizer.GetHighestSeverity(entries),
            RawEntries = entries,
            ChunkText = chunkText,
            Embedding = null // Will be filled on Day 4 when we add embedding generation
        };
    }

    /// <summary>
    /// Build a human-readable summary text for a chunk.
    /// This text is what gets embedded (converted to a vector) for semantic search.
    /// The better this text represents the chunk's content, the better the search results.
    /// </summary>
    private string BuildChunkText(
        string serviceName,
        DateTime windowStart,
        DateTime windowEnd,
        List<LogEntry> entries)
    {
        var lines = new List<string>
        {
            $"Service: {serviceName}",
            $"Time window: {windowStart:yyyy-MM-dd HH:mm} to {windowEnd:yyyy-MM-dd HH:mm}",
            $"Entry count: {entries.Count}",
            $"Highest severity: {_normalizer.GetHighestSeverity(entries)}",
            ""
        };

        foreach (var entry in entries)
        {
            lines.Add($"[{entry.Timestamp:HH:mm:ss}] [{entry.Severity}] {entry.Message}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Calculate the start of the time window that a given timestamp falls into.
    /// For example, with a 5-minute window:
    ///   3:07:23 -> 3:05:00
    ///   3:12:45 -> 3:10:00
    ///   3:00:00 -> 3:00:00
    ///
    /// This is like Python's: datetime.replace(minute=(minute // 5) * 5, second=0)
    /// </summary>
    private DateTime GetWindowStart(DateTime timestamp, int windowMinutes)
    {
        var totalMinutes = (int)timestamp.TimeOfDay.TotalMinutes;
        var windowIndex = totalMinutes / windowMinutes;
        var windowStartMinutes = windowIndex * windowMinutes;

        return timestamp.Date.AddMinutes(windowStartMinutes);
    }
}
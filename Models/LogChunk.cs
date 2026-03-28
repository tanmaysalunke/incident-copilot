namespace IncidentCopilot.Models;

// This represents a chunk of logs from a single service within a time window.
// In Python, this would be a Pydantic BaseModel.
public class LogChunk
{
    // Cosmos DB uses "id" as the unique identifier (lowercase, required)
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key: logs are grouped by service for efficient queries
    public string ServiceName { get; set; } = string.Empty;

    // Time window this chunk covers
    public DateTime TimeStart { get; set; }
    public DateTime TimeEnd { get; set; }

    // Highest severity in this chunk (DEBUG, INFO, WARN, ERROR, FATAL)
    public string Severity { get; set; } = "INFO";

    // The actual log entries in this chunk
    public List<LogEntry> RawEntries { get; set; } = new();

    // Vector embedding for semantic search (1536 dimensions for text-embedding-3-small)
    public float[]? Embedding { get; set; }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
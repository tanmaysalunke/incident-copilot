using Newtonsoft.Json;

namespace IncidentCopilot.Models;

public class LogChunk
{
    // Cosmos DB requires a lowercase "id" field
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key: logs are grouped by service for efficient queries
    [JsonProperty("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonProperty("timeStart")]
    public DateTime TimeStart { get; set; }

    [JsonProperty("timeEnd")]
    public DateTime TimeEnd { get; set; }

    [JsonProperty("severity")]
    public string Severity { get; set; } = "INFO";

    [JsonProperty("rawEntries")]
    public List<LogEntry> RawEntries { get; set; } = new();

    // Vector embedding for semantic search (1536 dimensions)
    [JsonProperty("embedding")]
    public float[]? Embedding { get; set; }

    // Summary text used to generate the embedding
    [JsonProperty("chunkText")]
    public string ChunkText { get; set; } = string.Empty;
}

public class LogEntry
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("service")]
    public string Service { get; set; } = string.Empty;

    [JsonProperty("severity")]
    public string Severity { get; set; } = "INFO";

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("traceId")]
    public string? TraceId { get; set; }

    [JsonProperty("spanId")]
    public string? SpanId { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
namespace IncidentCopilot.Models;

public class CosmosDbSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string LogChunksContainer { get; set; } = string.Empty;
    public string ServiceGraphContainer { get; set; } = string.Empty;
    public string ConversationsContainer { get; set; } = string.Empty;
    public string IncidentsContainer { get; set; } = string.Empty;

    // Vector search configuration
    public int EmbeddingDimensions { get; set; } = 1536; // text-embedding-3-small outputs 1536 dimensions
}
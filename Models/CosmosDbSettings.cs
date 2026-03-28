namespace IncidentCopilot.Models;

// This is like a Python dataclass or Pydantic model.
// It maps to the "CosmosDb" section in appsettings.json.
public class CosmosDbSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string LogChunksContainer { get; set; } = string.Empty;
    public string ServiceGraphContainer { get; set; } = string.Empty;
    public string ConversationsContainer { get; set; } = string.Empty;
    public string IncidentsContainer { get; set; } = string.Empty;
}
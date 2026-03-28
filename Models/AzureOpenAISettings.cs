namespace IncidentCopilot.Models;

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingDeployment { get; set; } = string.Empty;
    public string ChatDeployment { get; set; } = string.Empty;
}
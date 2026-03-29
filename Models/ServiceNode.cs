using Newtonsoft.Json;

namespace IncidentCopilot.Models;

public class ServiceNode
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("upstreamServices")]
    public List<string> UpstreamServices { get; set; } = new();

    [JsonProperty("downstreamServices")]
    public List<string> DownstreamServices { get; set; } = new();

    [JsonProperty("healthCheckUrl")]
    public string? HealthCheckUrl { get; set; }
}
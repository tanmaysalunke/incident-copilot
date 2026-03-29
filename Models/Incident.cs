using Newtonsoft.Json;

namespace IncidentCopilot.Models;

public class Incident
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("startTime")]
    public DateTime StartTime { get; set; }

    [JsonProperty("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonProperty("affectedServices")]
    public List<string> AffectedServices { get; set; } = new();

    [JsonProperty("status")]
    public string Status { get; set; } = "Active";

    [JsonProperty("rootCause")]
    public string? RootCause { get; set; }
}
namespace IncidentCopilot.Models;

public class Incident
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> AffectedServices { get; set; } = new();
    public string Status { get; set; } = "Active"; // Active, Investigating, Resolved
    public string? RootCause { get; set; }
}
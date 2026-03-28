namespace IncidentCopilot.Models;

// Represents a service in the dependency graph
public class ServiceNode
{
    public string Id { get; set; } = string.Empty; // Service name is the ID
    public List<string> UpstreamServices { get; set; } = new();
    public List<string> DownstreamServices { get; set; } = new();
    public string? HealthCheckUrl { get; set; }
}
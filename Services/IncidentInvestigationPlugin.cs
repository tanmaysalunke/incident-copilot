using System.ComponentModel;
using Microsoft.SemanticKernel;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Services;

/// <summary>
/// Semantic Kernel plugin that exposes investigation functions to the LLM.
///
/// When the LLM receives a question like "What caused the payment failures at 3am?",
/// it can decide to call these functions to gather evidence before answering.
/// For example, it might:
/// 1. Call SearchLogsByTimeRange to find logs around 3am
/// 2. Call GetServiceDependencies to understand which services are connected
/// 3. Call SearchLogsByService to check upstream services
/// 4. Synthesize all the evidence into a grounded answer
///
/// The [KernelFunction] attribute tells Semantic Kernel this method is callable.
/// The [Description] attribute tells the LLM what each function does.
/// </summary>
public class IncidentInvestigationPlugin
{
    private readonly RetrievalService _retrievalService;
    private readonly CosmosServiceGraphRepository _serviceRepo;
    private readonly CosmosIncidentRepository _incidentRepo;
    private readonly CosmosLogRepository _logRepo;

    public IncidentInvestigationPlugin(
        RetrievalService retrievalService,
        CosmosServiceGraphRepository serviceRepo,
        CosmosIncidentRepository incidentRepo,
        CosmosLogRepository logRepo)
    {
        _retrievalService = retrievalService;
        _serviceRepo = serviceRepo;
        _incidentRepo = incidentRepo;
        _logRepo = logRepo;
    }

    [KernelFunction("search_logs_by_time")]
    [Description("Search for log entries within a specific time range. Use this when the user mentions a specific time or asks about events during a particular period.")]
    public async Task<string> SearchLogsByTimeRange(
        [Description("The search query describing what to look for")] string query,
        [Description("Start of time range in ISO format (e.g., 2026-03-28T03:00:00Z)")] string timeStart,
        [Description("End of time range in ISO format (e.g., 2026-03-28T03:20:00Z)")] string timeEnd,
        [Description("Number of results to return")] int topK = 5)
    {
        var retrievalQuery = new RetrievalQuery
        {
            Question = query,
            TimeStart = DateTime.Parse(timeStart).ToUniversalTime(),
            TimeEnd = DateTime.Parse(timeEnd).ToUniversalTime(),
            TopK = topK
        };

        var result = await _retrievalService.RetrieveAsync(retrievalQuery);
        return FormatRetrievalResult(result);
    }

    [KernelFunction("search_logs_by_service")]
    [Description("Search for log entries from a specific service and its dependencies. Use this when the user asks about a particular service or wants to trace failures through the service chain.")]
    public async Task<string> SearchLogsByService(
        [Description("The search query describing what to look for")] string query,
        [Description("The service name to search (e.g., PaymentService, AuthService, PostgresDB)")] string serviceName,
        [Description("Number of results to return")] int topK = 5)
    {
        var retrievalQuery = new RetrievalQuery
        {
            Question = query,
            ServiceName = serviceName,
            TopK = topK
        };

        var result = await _retrievalService.RetrieveAsync(retrievalQuery);
        return FormatRetrievalResult(result);
    }

    [KernelFunction("get_service_dependencies")]
    [Description("Get the dependency graph showing how services are connected. Use this to understand which services depend on which, to trace cascading failures.")]
    public async Task<string> GetServiceDependencies(
        [Description("Optional: specific service name to get dependencies for. Leave empty for full graph.")] string? serviceName = null)
    {
        if (!string.IsNullOrEmpty(serviceName))
        {
            var service = await _serviceRepo.GetByIdAsync(serviceName);
            if (service == null)
                return $"Service '{serviceName}' not found in the dependency graph.";

            return $"Service: {service.Id}\n" +
                   $"Upstream (calls this service): {string.Join(", ", service.UpstreamServices)}\n" +
                   $"Downstream (this service calls): {string.Join(", ", service.DownstreamServices)}";
        }

        var allServices = await _serviceRepo.GetAllAsync();
        var lines = allServices.Select(s =>
            $"- {s.Id}: upstream=[{string.Join(", ", s.UpstreamServices)}], downstream=[{string.Join(", ", s.DownstreamServices)}]"
        );

        return "Service Dependency Graph:\n" + string.Join("\n", lines);
    }

    [KernelFunction("get_incident_details")]
    [Description("Get details about recorded incidents including affected services, status, and root cause if known.")]
    public async Task<string> GetIncidentDetails()
    {
        var incidents = await _incidentRepo.GetAllAsync();

        if (incidents.Count == 0)
            return "No incidents recorded.";

        var lines = incidents.Select(i =>
            $"Incident: {i.Title}\n" +
            $"  Time: {i.StartTime:yyyy-MM-dd HH:mm} to {(i.EndTime.HasValue ? i.EndTime.Value.ToString("HH:mm") : "ongoing")}\n" +
            $"  Status: {i.Status}\n" +
            $"  Affected Services: {string.Join(", ", i.AffectedServices)}\n" +
            $"  Root Cause: {i.RootCause ?? "Not yet determined"}"
        );

        return string.Join("\n\n", lines);
    }

    /// <summary>
    /// Format retrieval results into a string the LLM can reason over.
    /// Includes chunk IDs so the LLM can cite specific evidence.
    /// </summary>
    private string FormatRetrievalResult(RetrievalResult result)
    {
        if (result.Results.Count == 0)
            return "No relevant log entries found for this query.";

        var lines = new List<string>
        {
            $"Found {result.Results.Count} relevant log chunks (searched {result.TotalCandidates} candidates across services: {string.Join(", ", result.ServicesSearched)}):",
            ""
        };

        foreach (var scored in result.Results)
        {
            var chunk = scored.Chunk;
            var relatedTag = scored.IsFromRelatedService ? " [from related service]" : "";

            lines.Add($"--- Log Chunk [{chunk.Id}] ---");
            lines.Add($"Service: {chunk.ServiceName}{relatedTag}");
            lines.Add($"Time: {chunk.TimeStart:HH:mm} - {chunk.TimeEnd:HH:mm}");
            lines.Add($"Severity: {chunk.Severity} (score boost: {scored.SeverityWeight}x)");
            lines.Add($"Relevance: {scored.FinalScore:F4}");
            lines.Add($"Log entries:");
            lines.Add(chunk.ChunkText);
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}
namespace IncidentCopilot.Services;

using IncidentCopilot.Models;

/// <summary>
/// Converts heterogeneous log formats into the unified LogEntry schema.
/// In a real system, logs come from different sources (Kubernetes, cloud providers,
/// application frameworks) and each has its own format. This normalizer handles
/// the translation so the rest of the pipeline only deals with one format.
/// </summary>
public class LogNormalizer
{
    private readonly ILogger<LogNormalizer> _logger;

    // Valid severity levels, ordered from least to most severe
    private static readonly string[] ValidSeverities =
        { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };

    public LogNormalizer(ILogger<LogNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Normalize a batch of log entries: validate fields, fill defaults,
    /// and standardize severity levels.
    /// </summary>
    public List<LogEntry> Normalize(string serviceName, List<LogEntry> rawEntries)
    {
        var normalized = new List<LogEntry>();

        foreach (var entry in rawEntries)
        {
            var clean = new LogEntry
            {
                // Use the entry's timestamp, or current time if missing
                Timestamp = entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp,

                // Use the entry's service name, or fall back to the batch-level service name
                Service = string.IsNullOrEmpty(entry.Service) ? serviceName : entry.Service,

                // Normalize severity: uppercase, map common aliases
                Severity = NormalizeSeverity(entry.Severity),

                // Message is required
                Message = string.IsNullOrEmpty(entry.Message) ? "[empty message]" : entry.Message,

                // Optional fields pass through as-is
                TraceId = entry.TraceId,
                SpanId = entry.SpanId,
                Metadata = entry.Metadata
            };

            normalized.Add(clean);
        }

        _logger.LogInformation(
            "Normalized {Count} entries for service {Service}",
            normalized.Count, serviceName
        );

        return normalized;
    }

    /// <summary>
    /// Standardize severity strings. Handles common aliases like
    /// "warning" -> "WARN", "critical" -> "FATAL", "err" -> "ERROR".
    /// </summary>
    private string NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrEmpty(severity))
            return "INFO";

        var upper = severity.Trim().ToUpperInvariant();

        return upper switch
        {
            "DEBUG" or "TRACE" or "VERBOSE" => "DEBUG",
            "INFO" or "INFORMATION" => "INFO",
            "WARN" or "WARNING" => "WARN",
            "ERROR" or "ERR" => "ERROR",
            "FATAL" or "CRITICAL" or "EMERGENCY" or "ALERT" => "FATAL",
            _ => "INFO" // Default to INFO for unknown severity levels
        };
    }

    /// <summary>
    /// Determine the highest severity in a list of entries.
    /// Used to set the severity on a log chunk.
    /// </summary>
    public string GetHighestSeverity(List<LogEntry> entries)
    {
        var maxIndex = 0;

        foreach (var entry in entries)
        {
            var index = Array.IndexOf(ValidSeverities, entry.Severity);
            if (index > maxIndex) maxIndex = index;
        }

        return ValidSeverities[maxIndex];
    }
}
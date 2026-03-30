using System.ComponentModel.DataAnnotations;

namespace IncidentCopilot.Models;

// What the client sends when ingesting logs
public class IngestRequest
{
    [Required(ErrorMessage = "ServiceName is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "ServiceName must be between 1 and 100 characters")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Entries list is required")]
    public List<LogEntry> Entries { get; set; } = new();
}

// What the client sends when asking a question
public class InvestigateRequest
{
    [Required(ErrorMessage = "Question is required")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Question must be between 3 and 1000 characters")]
    public string Question { get; set; } = string.Empty;

    public DateTime? TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public string? SessionId { get; set; }
}

// Standard API response wrapper
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}
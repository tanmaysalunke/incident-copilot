namespace IncidentCopilot.Models;

// What the client sends when ingesting logs
public class IngestRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public List<LogEntry> Entries { get; set; } = new();
}

// What the client sends when asking a question
public class InvestigateRequest
{
    public string Question { get; set; } = string.Empty;
    public DateTime? TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public string? SessionId { get; set; } // For follow-up questions
}

// What the API returns for investigation queries
public class InvestigateResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<string> CitedLogChunkIds { get; set; } = new();
    public string SessionId { get; set; } = string.Empty;
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
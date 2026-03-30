using Serilog.Context;

namespace IncidentCopilot.Infrastructure;

/// <summary>
/// Adds a correlation ID to every request. This ID appears in all log
/// messages for that request, making it easy to trace a single request
/// through the entire system.
///
/// If the client sends an X-Correlation-ID header, we use that.
/// Otherwise, we generate a new one.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Use the client's correlation ID if provided, otherwise generate one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                           ?? Guid.NewGuid().ToString("N")[..12];

        // Add it to the response headers so the client can reference it
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Push it into Serilog's context so all log messages include it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
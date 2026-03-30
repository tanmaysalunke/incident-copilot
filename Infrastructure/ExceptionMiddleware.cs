using System.Net;
using System.Text.Json;

namespace IncidentCopilot.Infrastructure;

/// <summary>
/// Global exception handler that catches any unhandled exceptions
/// and returns a clean JSON error response instead of a stack trace.
///
/// In Python/FastAPI, this is like an exception_handler decorator.
/// In Django, this is like custom middleware that catches exceptions.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path
            );

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            _ => (HttpStatusCode.InternalServerError, "An internal error occurred. Please try again later.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            success = false,
            error = message,
            traceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }
}
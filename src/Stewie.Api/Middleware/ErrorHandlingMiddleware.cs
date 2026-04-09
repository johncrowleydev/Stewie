/// <summary>
/// Global error handling middleware — catches all unhandled exceptions and returns
/// a standardized JSON error response per CON-002 §6 and GOV-004.
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If API returns raw exceptions    → this middleware failed to catch them, check InvokeAsync
/// 2. If error format is wrong         → check the anonymous type in the catch blocks
/// 3. If 404s aren't formatted         → StatusCodePages config is missing in Program.cs
///
/// REF: CON-002 §6, GOV-004 §4
/// </summary>
using System.Net;
using System.Text.Json;

namespace Stewie.Api.Middleware;

/// <summary>
/// ASP.NET Core middleware that intercepts unhandled exceptions and converts them
/// into the standardized error response format: <c>{ "error": { "code", "message", "details" } }</c>.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    /// <summary>JSON serializer options for error responses — camelCase property naming.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes the error handling middleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger for structured error logging.</param>
    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware. Catches all exceptions from downstream middleware/controllers
    /// and converts them to structured error responses.
    ///
    /// FAILURE MODE: If this method itself throws, ASP.NET's built-in exception page
    /// will handle it. This is the last-resort safety net.
    /// BLAST RADIUS: All API consumers see raw error responses instead of structured ones.
    /// MITIGATION: This middleware has zero external dependencies by design.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            // Handle non-exception error status codes (e.g. 404 from routing)
            if (context.Response.StatusCode == (int)HttpStatusCode.NotFound
                && !context.Response.HasStarted)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.NotFound,
                    "NOT_FOUND",
                    "The requested resource was not found.");
            }
        }
        catch (ArgumentException ex)
        {
            // DECISION: ArgumentException maps to VALIDATION_ERROR (400).
            // This covers model binding failures and explicit validation throws.
            _logger.LogWarning(ex, "Validation error on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.BadRequest,
                "VALIDATION_ERROR",
                ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            // DECISION: KeyNotFoundException maps to NOT_FOUND (404).
            // Controllers throw this when an entity lookup returns null.
            _logger.LogWarning(ex, "Resource not found on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.NotFound,
                "NOT_FOUND",
                ex.Message);
        }
        catch (Exception ex)
        {
            // Catch-all for truly unexpected errors.
            // GOV-004 Law #2: Stack traces are sacred — log the full exception.
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Writes a standardized JSON error response per CON-002 §6.
    /// PRECONDITION: Response must not have started (HasStarted == false).
    /// </summary>
    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string errorCode,
        string message)
    {
        // Guard: Cannot write if response stream has already been flushed
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = new
            {
                code = errorCode,
                message,
                details = new { }
            }
        };

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}

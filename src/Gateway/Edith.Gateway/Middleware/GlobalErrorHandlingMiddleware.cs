using System.Text.Json;

namespace Edith.Gateway.Middleware;

public sealed partial class GlobalErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

    public GlobalErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "[{ReferenceCode}] Unhandled exception on {Method} {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception ex,
        string referenceCode,
        string method,
        string path);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var referenceCode = $"EDITH-ERR-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            LogUnhandledException(_logger, ex, referenceCode, context.Request.Method, context.Request.Path.Value ?? "/");

            if (context.Response.HasStarted)
            {
                throw;
            }

            var isBadRequest = ex is BadHttpRequestException or JsonException or ArgumentException;
            var statusCode = isBadRequest
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                errorCode = isBadRequest ? "VALIDATION_ERROR" : "SERVER_ERROR",
                referenceCode,
                title = isBadRequest ? "Invalid Information" : "Service Temporarily Unavailable",
                message = isBadRequest
                    ? "Some of the submitted information could not be processed. Please review your input and try again."
                    : "Something went wrong while processing your request. Please try again in a moment.",
                status = statusCode
            });
        }
    }
}

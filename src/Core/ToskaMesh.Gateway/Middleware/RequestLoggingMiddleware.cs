using System.Diagnostics;

namespace ToskaMesh.Gateway.Middleware;

/// <summary>
/// Middleware for logging incoming requests and outgoing responses.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.TraceIdentifier;

        _logger.LogInformation(
            "Incoming request: {Method} {Path} | Correlation-ID: {CorrelationId} | Client-IP: {ClientIp}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "Outgoing response: {Method} {Path} | Status: {StatusCode} | Duration: {ElapsedMs}ms | Correlation-ID: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId
            );
        }
    }
}

/// <summary>
/// Extension methods for RequestLoggingMiddleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}

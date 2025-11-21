using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ToskaMesh.Common.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions
/// and returns standardized error responses.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An internal server error occurred.";
        var errors = new List<string>();

        // Map specific exception types to appropriate status codes
        switch (exception)
        {
            case ArgumentException or ArgumentNullException:
                statusCode = HttpStatusCode.BadRequest;
                message = exception.Message;
                break;
            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                message = "Unauthorized access.";
                break;
            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                message = exception.Message;
                break;
            case InvalidOperationException:
                statusCode = HttpStatusCode.Conflict;
                message = exception.Message;
                break;
            case ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                message = "Validation failed.";
                errors = validationException.Errors.ToList();
                break;
            default:
                // For production, don't expose internal error details
                message = "An unexpected error occurred. Please try again later.";
                break;
        }

        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Errors = errors
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

/// <summary>
/// Custom validation exception for business logic validation failures.
/// </summary>
public class ValidationException : Exception
{
    public IEnumerable<string> Errors { get; }

    public ValidationException(string message, IEnumerable<string> errors)
        : base(message)
    {
        Errors = errors ?? Array.Empty<string>();
    }

    public ValidationException(string message)
        : this(message, new[] { message })
    {
    }
}

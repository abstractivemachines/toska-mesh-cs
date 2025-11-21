using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ToskaMesh.Common.Extensions;

/// <summary>
/// Extension methods for HttpContext operations.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the user ID from the current HTTP context claims.
    /// </summary>
    public static string? GetUserId(this HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Gets the user name from the current HTTP context claims.
    /// </summary>
    public static string? GetUserName(this HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.Name)?.Value;
    }

    /// <summary>
    /// Gets the user email from the current HTTP context claims.
    /// </summary>
    public static string? GetUserEmail(this HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets the user roles from the current HTTP context claims.
    /// </summary>
    public static IEnumerable<string> GetUserRoles(this HttpContext context)
    {
        return context.User?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    public static bool HasRole(this HttpContext context, string role)
    {
        return context.User?.IsInRole(role) ?? false;
    }

    /// <summary>
    /// Gets the client IP address from the HTTP context.
    /// </summary>
    public static string? GetClientIpAddress(this HttpContext context)
    {
        // Check for X-Forwarded-For header (when behind a proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP if there are multiple
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check for X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Gets the request correlation ID (or generates one if not present).
    /// </summary>
    public static string GetOrCreateCorrelationId(this HttpContext context)
    {
        const string correlationIdKey = "X-Correlation-ID";

        var correlationId = context.Request.Headers[correlationIdKey].FirstOrDefault();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Items[correlationIdKey] = correlationId;
        }

        return correlationId;
    }

    /// <summary>
    /// Sets the correlation ID in the response headers.
    /// </summary>
    public static void SetCorrelationIdHeader(this HttpContext context, string correlationId)
    {
        context.Response.Headers["X-Correlation-ID"] = correlationId;
    }
}

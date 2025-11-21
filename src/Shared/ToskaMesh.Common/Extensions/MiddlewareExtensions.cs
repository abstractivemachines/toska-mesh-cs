using Microsoft.AspNetCore.Builder;
using ToskaMesh.Common.Middleware;

namespace ToskaMesh.Common.Extensions;

/// <summary>
/// Extension methods for registering middleware components.
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Adds global exception handling middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ToksaMesh.Common.Extensions;

/// <summary>
/// Extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add common mesh utilities to the service collection
    /// </summary>
    public static IServiceCollection AddMeshCommon(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Add structured logging configuration
    /// </summary>
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
        });

        return services;
    }
}

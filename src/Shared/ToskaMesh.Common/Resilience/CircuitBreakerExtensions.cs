using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToskaMesh.Protocols;

namespace ToskaMesh.Common.Resilience;

/// <summary>
/// Extension methods for registering circuit breaker services.
/// </summary>
public static class CircuitBreakerExtensions
{
    /// <summary>
    /// Adds a named circuit breaker to the service collection.
    /// </summary>
    public static IServiceCollection AddCircuitBreaker(
        this IServiceCollection services,
        string name,
        Action<CircuitBreakerOptions>? configure = null)
    {
        var options = new CircuitBreakerOptions();
        configure?.Invoke(options);

        services.AddSingleton<ICircuitBreaker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PollyCircuitBreaker>>();
            return new PollyCircuitBreaker(name, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a named circuit breaker to the service collection using configuration.
    /// </summary>
    public static IServiceCollection AddCircuitBreaker(
        this IServiceCollection services,
        string name,
        IConfiguration configuration)
    {
        var options = new CircuitBreakerOptions();
        configuration.GetSection(CircuitBreakerOptions.SectionName).Bind(options);

        services.AddSingleton<ICircuitBreaker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PollyCircuitBreaker>>();
            return new PollyCircuitBreaker(name, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a circuit breaker factory for creating named circuit breakers on demand.
    /// </summary>
    public static IServiceCollection AddCircuitBreakerFactory(
        this IServiceCollection services,
        Action<CircuitBreakerOptions>? configureDefaults = null)
    {
        var defaultOptions = new CircuitBreakerOptions();
        configureDefaults?.Invoke(defaultOptions);

        services.AddSingleton(defaultOptions);
        services.AddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();

        return services;
    }
}

/// <summary>
/// Factory interface for creating named circuit breakers.
/// </summary>
public interface ICircuitBreakerFactory
{
    /// <summary>
    /// Gets or creates a circuit breaker with the specified name.
    /// </summary>
    ICircuitBreaker GetOrCreate(string name, Action<CircuitBreakerOptions>? configure = null);
}

/// <summary>
/// Factory for creating and caching named circuit breakers.
/// </summary>
public class CircuitBreakerFactory : ICircuitBreakerFactory
{
    private readonly CircuitBreakerOptions _defaultOptions;
    private readonly ILogger<PollyCircuitBreaker> _logger;
    private readonly Dictionary<string, ICircuitBreaker> _breakers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public CircuitBreakerFactory(
        CircuitBreakerOptions defaultOptions,
        ILogger<PollyCircuitBreaker> logger)
    {
        _defaultOptions = defaultOptions;
        _logger = logger;
    }

    public ICircuitBreaker GetOrCreate(string name, Action<CircuitBreakerOptions>? configure = null)
    {
        lock (_lock)
        {
            if (_breakers.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var options = new CircuitBreakerOptions
            {
                FailureRatio = _defaultOptions.FailureRatio,
                SamplingDuration = _defaultOptions.SamplingDuration,
                MinimumThroughput = _defaultOptions.MinimumThroughput,
                BreakDuration = _defaultOptions.BreakDuration
            };

            configure?.Invoke(options);

            var breaker = new PollyCircuitBreaker(name, options, _logger);
            _breakers[name] = breaker;
            return breaker;
        }
    }
}

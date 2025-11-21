using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ToskaMesh.Common.Messaging;

/// <summary>
/// Extension methods for configuring MassTransit with RabbitMQ.
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ to the service collection.
    /// </summary>
    public static IServiceCollection AddMeshMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var messagingConfig = new MessagingConfiguration();
        configuration.GetSection("Messaging").Bind(messagingConfig);

        services.AddMassTransit(x =>
        {
            // Allow consumers to be registered
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(messagingConfig.RabbitMqHost, messagingConfig.RabbitMqVirtualHost, h =>
                {
                    h.Username(messagingConfig.RabbitMqUsername);
                    h.Password(messagingConfig.RabbitMqPassword);
                });

                // Configure message retry
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                ));

                // Configure circuit breaker
                cfg.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                    cb.TripThreshold = 15;
                    cb.ActiveThreshold = 10;
                    cb.ResetInterval = TimeSpan.FromMinutes(5);
                });

                // Configure rate limiting
                cfg.UseRateLimit(1000, TimeSpan.FromSeconds(1));

                // Configure endpoints
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

/// <summary>
/// Configuration for messaging infrastructure.
/// </summary>
public class MessagingConfiguration
{
    public string RabbitMqHost { get; set; } = "localhost";
    public string RabbitMqVirtualHost { get; set; } = "/";
    public string RabbitMqUsername { get; set; } = "guest";
    public string RabbitMqPassword { get; set; } = "guest";
}

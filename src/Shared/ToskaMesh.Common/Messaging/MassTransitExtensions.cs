using Amazon.SimpleNotificationService;
using Amazon.SQS;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ToskaMesh.Common.Messaging;

/// <summary>
/// Extension methods for configuring MassTransit transports.
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with the configured transport to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration containing Messaging section.</param>
    /// <param name="configureConsumers">Optional action to configure message consumers.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="MessagingConfigurationException">Thrown when configuration is invalid.</exception>
    public static IServiceCollection AddMeshMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var messagingConfig = new MessagingConfiguration();
        configuration.GetSection("Messaging").Bind(messagingConfig);

        // Validate configuration before proceeding
        messagingConfig.Validate();

        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            void ConfigurePipeline(IBusFactoryConfigurator cfg)
            {
                cfg.UseInstrumentation();

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                ));

                cfg.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                    cb.TripThreshold = 15;
                    cb.ActiveThreshold = 10;
                    cb.ResetInterval = TimeSpan.FromMinutes(5);
                });

                cfg.UseRateLimit(1000, TimeSpan.FromSeconds(1));
            }

            switch (messagingConfig.Transport)
            {
                case MessagingTransport.RabbitMq:
                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(messagingConfig.RabbitMqHost, messagingConfig.RabbitMqVirtualHost, h =>
                        {
                            h.Username(messagingConfig.RabbitMqUsername);
                            h.Password(messagingConfig.RabbitMqPassword);
                        });

                        ConfigurePipeline(cfg);
                        cfg.ConfigureEndpoints(context);
                    });
                    break;

                case MessagingTransport.AwsSqs:
                    ConfigureAwsSqs(x, messagingConfig.AwsSqs, ConfigurePipeline);
                    break;

                default:
                    // This should never happen due to Validate() call above
                    throw new UnsupportedMessagingTransportException(messagingConfig.Transport);
            }
        });

        return services;
    }

    private static void ConfigureAwsSqs(
        IBusRegistrationConfigurator x,
        AwsSqsConfiguration awsConfig,
        Action<IBusFactoryConfigurator> configurePipeline)
    {
        x.UsingAmazonSqs((context, cfg) =>
        {
            cfg.Host(awsConfig.Region, h =>
            {
                // Only configure explicit credentials if provided.
                // Otherwise, AWS SDK uses default credential provider chain
                // (IAM roles, environment variables, shared credentials file, etc.)
                if (awsConfig.HasExplicitCredentials)
                {
                    h.AccessKey(awsConfig.AccessKeyId!);
                    h.SecretKey(awsConfig.SecretAccessKey!);
                }

                if (!string.IsNullOrWhiteSpace(awsConfig.Scope))
                {
                    h.Scope(awsConfig.Scope);
                }

                // ServiceUrl is typically only used for LocalStack/local development
                if (!string.IsNullOrWhiteSpace(awsConfig.ServiceUrl))
                {
                    h.Config(new AmazonSimpleNotificationServiceConfig
                    {
                        ServiceURL = awsConfig.ServiceUrl
                    });

                    h.Config(new AmazonSQSConfig
                    {
                        ServiceURL = awsConfig.ServiceUrl
                    });
                }
            });

            configurePipeline(cfg);
            cfg.ConfigureEndpoints(context);
        });
    }
}

/// <summary>
/// Configuration for messaging infrastructure.
/// </summary>
public class MessagingConfiguration
{
    /// <summary>
    /// The messaging transport to use. Defaults to RabbitMQ.
    /// </summary>
    public MessagingTransport Transport { get; set; } = MessagingTransport.RabbitMq;

    /// <summary>
    /// RabbitMQ host address.
    /// </summary>
    public string RabbitMqHost { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ virtual host.
    /// </summary>
    public string RabbitMqVirtualHost { get; set; } = "/";

    /// <summary>
    /// RabbitMQ username.
    /// </summary>
    public string RabbitMqUsername { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ password.
    /// </summary>
    public string RabbitMqPassword { get; set; } = "guest";

    /// <summary>
    /// AWS SNS/SQS configuration. Only used when Transport is AwsSqs.
    /// </summary>
    public AwsSqsConfiguration AwsSqs { get; set; } = new();

    /// <summary>
    /// Validates the messaging configuration based on the selected transport.
    /// </summary>
    /// <exception cref="UnsupportedMessagingTransportException">Thrown when transport is not supported.</exception>
    /// <exception cref="MessagingConfigurationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(MessagingTransport), Transport))
        {
            throw new UnsupportedMessagingTransportException(Transport);
        }

        switch (Transport)
        {
            case MessagingTransport.RabbitMq:
                ValidateRabbitMq();
                break;

            case MessagingTransport.AwsSqs:
                AwsSqs.Validate();
                break;

            default:
                throw new UnsupportedMessagingTransportException(Transport);
        }
    }

    private void ValidateRabbitMq()
    {
        if (string.IsNullOrWhiteSpace(RabbitMqHost))
        {
            throw new MessagingConfigurationException(
                "RabbitMqHost is required when using RabbitMQ transport. " +
                "Set Messaging:RabbitMqHost in configuration.");
        }
    }
}

/// <summary>
/// AWS SNS/SQS messaging configuration.
/// <para>
/// <strong>Security Warning:</strong> Prefer using IAM roles, instance profiles, or environment
/// variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY) for credentials instead of hardcoding
/// AccessKeyId and SecretAccessKey in configuration files. The AWS SDK will automatically use
/// the default credential provider chain when explicit credentials are not provided.
/// </para>
/// <para>
/// The ServiceUrl property should only be used for local development with LocalStack or similar
/// tools. Never set ServiceUrl in production environments.
/// </para>
/// </summary>
public class AwsSqsConfiguration
{
    // Common AWS regions for validation hints
    private static readonly HashSet<string> KnownAwsRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        // US regions
        "us-east-1", "us-east-2", "us-west-1", "us-west-2",
        // EU regions
        "eu-west-1", "eu-west-2", "eu-west-3", "eu-central-1", "eu-central-2",
        "eu-north-1", "eu-south-1", "eu-south-2",
        // Asia Pacific regions
        "ap-south-1", "ap-south-2", "ap-northeast-1", "ap-northeast-2", "ap-northeast-3",
        "ap-southeast-1", "ap-southeast-2", "ap-southeast-3", "ap-southeast-4", "ap-east-1",
        // Other regions
        "sa-east-1", "ca-central-1", "ca-west-1", "me-south-1", "me-central-1",
        "af-south-1", "il-central-1",
        // GovCloud
        "us-gov-west-1", "us-gov-east-1",
        // China
        "cn-north-1", "cn-northwest-1"
    };

    /// <summary>
    /// AWS region for SNS/SQS (e.g., "us-east-1", "eu-west-1").
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Optional scope/prefix for queue and topic names. Useful for environment isolation.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// AWS Access Key ID. 
    /// <para>
    /// <strong>Security Warning:</strong> Prefer IAM roles or environment variables over
    /// hardcoding credentials. Only use for local development or testing scenarios.
    /// </para>
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS Secret Access Key.
    /// <para>
    /// <strong>Security Warning:</strong> Prefer IAM roles or environment variables over
    /// hardcoding credentials. Only use for local development or testing scenarios.
    /// </para>
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Optional custom service URL for SNS/SQS endpoints.
    /// <para>
    /// <strong>Warning:</strong> Only use for local development with LocalStack or similar tools.
    /// Do not set in production environments.
    /// </para>
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Validates the AWS SQS configuration.
    /// </summary>
    /// <exception cref="MessagingConfigurationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        ValidateRegion();
        ValidateCredentials();
        ValidateServiceUrl();
    }

    private void ValidateRegion()
    {
        if (string.IsNullOrWhiteSpace(Region))
        {
            throw new MessagingConfigurationException(
                "AWS SQS Region is required. Set Messaging:AwsSqs:Region in configuration. " +
                $"Example regions: us-east-1, eu-west-1, ap-southeast-1");
        }

        // Warn about potentially invalid region (but don't fail - new regions may exist)
        if (!KnownAwsRegions.Contains(Region) && !Region.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            // Region format should be like: xx-xxxx-N (e.g., us-east-1, eu-west-2)
            var regionPattern = System.Text.RegularExpressions.Regex.IsMatch(
                Region, @"^[a-z]{2}-[a-z]+-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!regionPattern)
            {
                throw new MessagingConfigurationException(
                    $"Invalid AWS region format: '{Region}'. " +
                    $"Expected format like 'us-east-1' or 'eu-west-2'. " +
                    $"Common regions: {string.Join(", ", KnownAwsRegions.Take(6))}");
            }
        }
    }

    private void ValidateCredentials()
    {
        var hasAccessKey = !string.IsNullOrWhiteSpace(AccessKeyId);
        var hasSecretKey = !string.IsNullOrWhiteSpace(SecretAccessKey);

        if (hasAccessKey != hasSecretKey)
        {
            throw new MessagingConfigurationException(
                "Both AccessKeyId and SecretAccessKey must be provided together, or neither. " +
                "Prefer using IAM roles or environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY) for credentials.");
        }

        // Validate access key format if provided (should be 16-128 alphanumeric characters)
        if (hasAccessKey && (AccessKeyId!.Length < 16 || AccessKeyId.Length > 128))
        {
            throw new MessagingConfigurationException(
                $"Invalid AccessKeyId format. AWS access keys are typically 20 characters. " +
                $"Provided key length: {AccessKeyId.Length}");
        }

        // Validate secret key format if provided (should be 40 characters for standard keys)
        if (hasSecretKey && SecretAccessKey!.Length < 20)
        {
            throw new MessagingConfigurationException(
                $"Invalid SecretAccessKey format. AWS secret keys are typically 40 characters. " +
                $"Provided key length: {SecretAccessKey.Length}");
        }
    }

    private void ValidateServiceUrl()
    {
        if (string.IsNullOrWhiteSpace(ServiceUrl))
        {
            return;
        }

        if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var uri))
        {
            throw new MessagingConfigurationException(
                $"Invalid ServiceUrl '{ServiceUrl}'. Must be a valid absolute URL. " +
                $"Example for LocalStack: http://localhost:4566");
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new MessagingConfigurationException(
                $"Invalid ServiceUrl scheme '{uri.Scheme}'. Must be 'http' or 'https'. " +
                $"Example for LocalStack: http://localhost:4566");
        }

        // Warn if using HTTP with non-localhost (potential security issue)
        if (uri.Scheme == "http" && !IsLocalhost(uri.Host))
        {
            // This is a warning scenario - we allow it but the docs warn against it
            // In a real production scenario, you might want to log a warning here
        }
    }

    private static bool IsLocalhost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("127.0.0.1", StringComparison.Ordinal) ||
        host.Equals("::1", StringComparison.Ordinal);

    /// <summary>
    /// Returns true if explicit credentials are configured.
    /// When false, the AWS SDK will use the default credential provider chain.
    /// </summary>
    public bool HasExplicitCredentials =>
        !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey);

    /// <summary>
    /// Returns true if a custom service URL is configured (typically for LocalStack).
    /// </summary>
    public bool HasCustomServiceUrl => !string.IsNullOrWhiteSpace(ServiceUrl);
}

/// <summary>
/// Exception thrown when messaging configuration is invalid.
/// </summary>
public class MessagingConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Creates a new MessagingConfigurationException with the specified message.
    /// </summary>
    public MessagingConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new MessagingConfigurationException with the specified message and inner exception.
    /// </summary>
    public MessagingConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an unsupported messaging transport is configured.
/// </summary>
public class UnsupportedMessagingTransportException : MessagingConfigurationException
{
    /// <summary>
    /// Creates a new UnsupportedMessagingTransportException for the specified transport.
    /// </summary>
    /// <param name="transport">The unsupported transport value.</param>
    public UnsupportedMessagingTransportException(MessagingTransport transport)
        : base($"Unsupported messaging transport: {transport}. " +
               $"Supported transports: {string.Join(", ", Enum.GetNames<MessagingTransport>())}")
    {
        Transport = transport;
    }

    /// <summary>
    /// Creates a new UnsupportedMessagingTransportException for an invalid transport string.
    /// </summary>
    /// <param name="transportValue">The invalid transport string value.</param>
    public UnsupportedMessagingTransportException(string transportValue)
        : base($"Invalid messaging transport value: '{transportValue}'. " +
               $"Supported transports: {string.Join(", ", Enum.GetNames<MessagingTransport>())}")
    {
        TransportValue = transportValue;
    }

    /// <summary>
    /// The unsupported transport enum value, if available.
    /// </summary>
    public MessagingTransport? Transport { get; }

    /// <summary>
    /// The invalid transport string value, if the value could not be parsed as an enum.
    /// </summary>
    public string? TransportValue { get; }
}

/// <summary>
/// Supported messaging transports.
/// </summary>
public enum MessagingTransport
{
    /// <summary>
    /// RabbitMQ message broker.
    /// </summary>
    RabbitMq,

    /// <summary>
    /// AWS SNS/SQS messaging services.
    /// </summary>
    AwsSqs
}

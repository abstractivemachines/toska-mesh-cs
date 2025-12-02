using System;
using Google.Protobuf.WellKnownTypes;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using ToskaMesh.Grpc.Discovery;
using ToskaMesh.Protocols;
using ToskaMesh.Security;
using MeshHealthStatus = ToskaMesh.Protocols.HealthStatus;
using MeshServiceInstance = ToskaMesh.Protocols.ServiceInstance;
using ProtoHealthStatus = ToskaMesh.Grpc.Discovery.HealthStatus;
using ProtoServiceInstance = ToskaMesh.Grpc.Discovery.ServiceInstance;

namespace ToskaMesh.Common.ServiceDiscovery;

/// <summary>
/// gRPC-based implementation of <see cref="IServiceRegistry"/> that talks to the Discovery service.
/// </summary>
public class GrpcServiceRegistry : IServiceRegistry
{
    private readonly DiscoveryRegistry.DiscoveryRegistryClient _client;
    private readonly ILogger<GrpcServiceRegistry> _logger;

    public GrpcServiceRegistry(
        DiscoveryRegistry.DiscoveryRegistryClient client,
        ILogger<GrpcServiceRegistry> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ServiceRegistrationResult> RegisterAsync(ServiceRegistration registration, CancellationToken cancellationToken = default)
    {
        var request = new RegisterServiceRequest
        {
            ServiceName = registration.ServiceName,
            ServiceId = registration.ServiceId,
            Address = registration.Address,
            Port = registration.Port
        };

        if (registration.Metadata != null)
        {
            foreach (var kvp in registration.Metadata)
            {
                request.Metadata[kvp.Key] = kvp.Value;
            }
        }

        if (registration.HealthCheck != null)
        {
            request.HealthCheck = new HealthCheckConfig
            {
                Endpoint = registration.HealthCheck.Endpoint,
                IntervalSeconds = (int)registration.HealthCheck.Interval.TotalSeconds,
                TimeoutSeconds = (int)registration.HealthCheck.Timeout.TotalSeconds,
                UnhealthyThreshold = registration.HealthCheck.UnhealthyThreshold
            };
        }

        var response = await _client.RegisterAsync(request, cancellationToken: cancellationToken);
        return new ServiceRegistrationResult(response.Success, response.ServiceId, response.ErrorMessage);
    }

    public async Task<bool> DeregisterAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeregisterAsync(new DeregisterServiceRequest { ServiceId = serviceId }, cancellationToken: cancellationToken);
        return response.Removed;
    }

    public async Task<IEnumerable<MeshServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetInstancesAsync(new GetInstancesRequest { ServiceName = serviceName }, cancellationToken: cancellationToken);
        return response.Instances.Select(MapInstance).ToList();
    }

    public async Task<MeshServiceInstance?> GetServiceInstanceAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var all = await _client.GetServicesAsync(new GetServicesRequest(), cancellationToken: cancellationToken);
        foreach (var service in all.ServiceNames)
        {
            var instances = await GetServiceInstancesAsync(service, cancellationToken);
            var match = instances.FirstOrDefault(instance => instance.ServiceId == serviceId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public async Task<IEnumerable<string>> GetAllServicesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.GetServicesAsync(new GetServicesRequest(), cancellationToken: cancellationToken);
        return response.ServiceNames;
    }

    public async Task<bool> UpdateHealthStatusAsync(string serviceId, MeshHealthStatus status, CancellationToken cancellationToken = default)
    {
        var response = await _client.ReportHealthAsync(new ReportHealthRequest
        {
            ServiceId = serviceId,
            Status = MapStatus(status)
        }, cancellationToken: cancellationToken);

        return response.Success;
    }

    private static MeshServiceInstance MapInstance(ProtoServiceInstance instance)
    {
        return new MeshServiceInstance(
            instance.ServiceName,
            instance.ServiceId,
            instance.Address,
            instance.Port,
            MapStatus(instance.Status),
            instance.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value),
            instance.RegisteredAt.ToDateTime(),
            instance.LastHealthCheck.ToDateTime());
    }

    private static MeshHealthStatus MapStatus(ProtoHealthStatus status) =>
        status switch
        {
            ProtoHealthStatus.Healthy => MeshHealthStatus.Healthy,
            ProtoHealthStatus.Unhealthy => MeshHealthStatus.Unhealthy,
            ProtoHealthStatus.Degraded => MeshHealthStatus.Degraded,
            _ => MeshHealthStatus.Unknown
        };

    private static ProtoHealthStatus MapStatus(MeshHealthStatus status) =>
        status switch
        {
            MeshHealthStatus.Healthy => ProtoHealthStatus.Healthy,
            MeshHealthStatus.Unhealthy => ProtoHealthStatus.Unhealthy,
            MeshHealthStatus.Degraded => ProtoHealthStatus.Degraded,
            _ => ProtoHealthStatus.Unknown
        };
}

/// <summary>
/// Options for configuring the gRPC service registry client.
/// </summary>
public class ServiceDiscoveryGrpcOptions
{
    public string Address { get; set; } = "https://localhost:5010";
    public bool AllowInsecureTransport { get; set; }
    public string ClientCertificatePath { get; set; } = string.Empty;
    public string ClientCertificatePassword { get; set; } = string.Empty;
}

/// <summary>
/// Provides call credentials for discovery gRPC calls (e.g., tokens).
/// </summary>
public interface IServiceDiscoveryCredentialsProvider
{
    Task PopulateAsync(Metadata metadata, CancellationToken cancellationToken);
}

internal sealed class JwtServiceDiscoveryCredentialsProvider : IServiceDiscoveryCredentialsProvider
{
    private readonly IMeshServiceTokenProvider _tokenProvider;

    public JwtServiceDiscoveryCredentialsProvider(IMeshServiceTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public async Task PopulateAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        metadata.Add("Authorization", $"Bearer {token}");
    }
}

public static class GrpcServiceRegistryExtensions
{
    public static IServiceCollection AddGrpcServiceRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        var grpcOptions = configuration.GetSection("Mesh:ServiceDiscovery:Grpc").Get<ServiceDiscoveryGrpcOptions>()
            ?? new ServiceDiscoveryGrpcOptions();
        var address = new Uri(grpcOptions.Address);
        var usesHttps = address.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!usesHttps && !grpcOptions.AllowInsecureTransport)
        {
            throw new InvalidOperationException("Mesh:ServiceDiscovery:Grpc must use https when sending credentials. Set AllowInsecureTransport=true only for local dev.");
        }

        services.AddMeshServiceIdentity(configuration);
        services.TryAddSingleton<IServiceDiscoveryCredentialsProvider, JwtServiceDiscoveryCredentialsProvider>();

        services.AddGrpcClient<DiscoveryRegistry.DiscoveryRegistryClient>((provider, options) =>
        {
            options.Address = address;
        }).AddCallCredentials(async (context, metadata, provider) =>
        {
            var credProvider = provider.GetService<IServiceDiscoveryCredentialsProvider>();
            if (credProvider != null)
            {
                await credProvider.PopulateAsync(metadata, context.CancellationToken);
            }
        }).ConfigureChannel(options =>
        {
            options.UnsafeUseInsecureChannelCallCredentials = !usesHttps && grpcOptions.AllowInsecureTransport;
            if (usesHttps && !string.IsNullOrWhiteSpace(grpcOptions.ClientCertificatePath))
            {
                try
                {
                    var cert = new X509Certificate2(grpcOptions.ClientCertificatePath, grpcOptions.ClientCertificatePassword);
                    var handler = new SocketsHttpHandler();
                    handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
                    options.HttpHandler = handler;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to load discovery client certificate from {grpcOptions.ClientCertificatePath}", ex);
                }
            }
        });

        services.AddSingleton<IServiceRegistry, GrpcServiceRegistry>();
        return services;
    }
}

using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using ToskaMesh.Discovery.Services;
using ToskaMesh.Grpc.Discovery;
using ToskaMesh.Protocols;
using ToskaMesh.Security;
using System.Net;

namespace ToskaMesh.Discovery.Grpc;

[Authorize(Policy = MeshAuthorizationPolicies.RequireServiceRole)]
public class DiscoveryGrpcService : DiscoveryRegistry.DiscoveryRegistryBase
{
    private readonly IServiceManager _serviceManager;
    private readonly ILogger<DiscoveryGrpcService> _logger;

    public DiscoveryGrpcService(IServiceManager serviceManager, ILogger<DiscoveryGrpcService> logger)
    {
        _serviceManager = serviceManager;
        _logger = logger;
    }

    public override async Task<RegisterServiceResponse> Register(RegisterServiceRequest request, ServerCallContext context)
    {
        var advertisedAddress = ResolveAdvertisedAddress(request, context);

        var registration = new ServiceRegistration(
            request.ServiceName,
            string.IsNullOrWhiteSpace(request.ServiceId) ? Guid.NewGuid().ToString("N") : request.ServiceId,
            advertisedAddress,
            request.Port,
            request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value),
            request.HealthCheck == null
                ? null
                : new HealthCheckConfiguration(
                    request.HealthCheck.Endpoint,
                    TimeSpan.FromSeconds(Math.Max(5, request.HealthCheck.IntervalSeconds)),
                    TimeSpan.FromSeconds(Math.Max(1, request.HealthCheck.TimeoutSeconds)),
                    request.HealthCheck.UnhealthyThreshold <= 0 ? 3 : request.HealthCheck.UnhealthyThreshold));

        var result = await _serviceManager.RegisterAsync(registration, context.CancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Failed to register service {ServiceName}: {Error}", request.ServiceName, result.ErrorMessage);
        }

        return new RegisterServiceResponse
        {
            Success = result.Success,
            ServiceId = result.ServiceId,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        };
    }

    private string ResolveAdvertisedAddress(RegisterServiceRequest request, ServerCallContext context)
    {
        var requested = request.Address?.Trim();
        if (IsRoutableAddress(requested, out _))
        {
            return requested!;
        }

        var remote = GetRemoteIp(context);
        if (remote != null && IsRoutableAddress(remote.ToString(), out _))
        {
            var resolved = remote.ToString();
            _logger.LogInformation(
                "Normalized service address for {Service} from '{Requested}' to caller IP {Resolved}",
                request.ServiceName,
                string.IsNullOrWhiteSpace(requested) ? "(none)" : requested,
                resolved);
            return resolved;
        }

        _logger.LogWarning(
            "Could not resolve a routable address for {Service}; keeping requested value '{Requested}'",
            request.ServiceName,
            string.IsNullOrWhiteSpace(requested) ? "(none)" : requested);

        return requested ?? string.Empty;
    }

    private static bool IsRoutableAddress(string? value, out IPAddress? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value, out var ip))
        {
            // Allow hostnames to pass through.
            return true;
        }

        parsed = ip;
        var target = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

        if (IPAddress.IsLoopback(target))
        {
            return false;
        }

        if (target.Equals(IPAddress.Any) || target.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        return true;
    }

    private static IPAddress? GetRemoteIp(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var remote = httpContext?.Connection.RemoteIpAddress;
        if (remote?.IsIPv4MappedToIPv6 == true)
        {
            remote = remote.MapToIPv4();
        }

        if (remote != null)
        {
            return remote;
        }

        var peer = context.Peer;
        if (string.IsNullOrWhiteSpace(peer) || !peer.StartsWith("ipv4:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var withoutPrefix = peer.Substring("ipv4:".Length);
        var colonIndex = withoutPrefix.LastIndexOf(':');
        var addressPart = colonIndex > 0 ? withoutPrefix[..colonIndex] : withoutPrefix;

        return IPAddress.TryParse(addressPart, out var peerIp)
            ? (peerIp.IsIPv4MappedToIPv6 ? peerIp.MapToIPv4() : peerIp)
            : null;
    }

    public override async Task<DeregisterServiceResponse> Deregister(DeregisterServiceRequest request, ServerCallContext context)
    {
        var removed = await _serviceManager.DeregisterAsync(request.ServiceId, context.CancellationToken);
        return new DeregisterServiceResponse { Removed = removed };
    }

    public override async Task<GetInstancesResponse> GetInstances(GetInstancesRequest request, ServerCallContext context)
    {
        var instances = await _serviceManager.GetInstancesAsync(request.ServiceName, context.CancellationToken);
        var response = new GetInstancesResponse();

        foreach (var instance in instances)
        {
            response.Instances.Add(instance.ToGrpc());
        }

        return response;
    }

    public override async Task<GetServicesResponse> GetServices(GetServicesRequest request, ServerCallContext context)
    {
        var services = await _serviceManager.GetServiceNamesAsync(context.CancellationToken);
        var response = new GetServicesResponse();
        response.ServiceNames.AddRange(services);
        return response;
    }

    public override async Task<ReportHealthResponse> ReportHealth(ReportHealthRequest request, ServerCallContext context)
    {
        var status = request.Status switch
        {
            ToskaMesh.Grpc.Discovery.HealthStatus.Healthy => ToskaMesh.Protocols.HealthStatus.Healthy,
            ToskaMesh.Grpc.Discovery.HealthStatus.Unhealthy => ToskaMesh.Protocols.HealthStatus.Unhealthy,
            ToskaMesh.Grpc.Discovery.HealthStatus.Degraded => ToskaMesh.Protocols.HealthStatus.Degraded,
            _ => ToskaMesh.Protocols.HealthStatus.Unknown
        };

        var updated = await _serviceManager.UpdateHealthAsync(request.ServiceId, status, request.Output, context.CancellationToken);
        return new ReportHealthResponse { Success = updated };
    }
}

internal static class DiscoveryGrpcMappingExtensions
{
    public static ToskaMesh.Grpc.Discovery.ServiceInstance ToGrpc(this ToskaMesh.Protocols.ServiceInstance instance)
    {
        var grpcInstance = new ToskaMesh.Grpc.Discovery.ServiceInstance
        {
            ServiceName = instance.ServiceName,
            ServiceId = instance.ServiceId,
            Address = instance.Address,
            Port = instance.Port,
            Status = instance.Status switch
            {
                ToskaMesh.Protocols.HealthStatus.Healthy => ToskaMesh.Grpc.Discovery.HealthStatus.Healthy,
                ToskaMesh.Protocols.HealthStatus.Unhealthy => ToskaMesh.Grpc.Discovery.HealthStatus.Unhealthy,
                ToskaMesh.Protocols.HealthStatus.Degraded => ToskaMesh.Grpc.Discovery.HealthStatus.Degraded,
                _ => ToskaMesh.Grpc.Discovery.HealthStatus.Unknown
            },
            RegisteredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(instance.RegisteredAt, DateTimeKind.Utc)),
            LastHealthCheck = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(instance.LastHealthCheck, DateTimeKind.Utc))
        };

        foreach (var kvp in instance.Metadata)
        {
            grpcInstance.Metadata[kvp.Key] = kvp.Value;
        }

        return grpcInstance;
    }
}

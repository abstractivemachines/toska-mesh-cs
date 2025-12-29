using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace ToskaMesh.Common.Messaging;

public interface IMeshRpc
{
    Task<TResponse> CallAsync<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : class
        where TResponse : class;
}

public sealed class MeshRpc : IMeshRpc
{
    private readonly IServiceProvider _services;

    public MeshRpc(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<TResponse> CallAsync<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : class
        where TResponse : class
    {
        var client = _services.GetRequiredService<IRequestClient<TRequest>>();
        var response = await client.GetResponse<TResponse>(request, ct);
        return response.Message;
    }
}

public static class MeshRpcServiceCollectionExtensions
{
    public static IServiceCollection AddMeshRpc(this IServiceCollection services)
    {
        services.AddScoped<IMeshRpc, MeshRpc>();
        return services;
    }
}

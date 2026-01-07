using MassTransit;
using MeshRpcDemo.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Common.Messaging;
using ToskaMesh.Runtime;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await MeshLambdaService.RunAsync(
    app =>
    {
        app.MapGet("/health", () => Results.Ok("ok"));
    },
    options =>
    {
        options.ServiceName = "mesh-rpc-b";
        options.Port = 8082;
        options.Routing.HealthCheckEndpoint = "/health";
        // Let configuration control these settings
        // options.AllowNoopServiceRegistry = true;
        // options.RegisterAutomatically = false;
        // options.EnableAuth = false;
        // options.EnableTelemetry = false;
    },
    services =>
    {
        services.AddMeshMassTransit(configuration, x =>
        {
            x.AddConsumer<StepBConsumer>();
            x.AddRequestClient<StepCRequest>();
        });

        services.AddMeshRpc();
    });

public sealed class StepBConsumer : IConsumer<StepBRequest>
{
    private readonly IMeshRpc _rpc;

    public StepBConsumer(IMeshRpc rpc)
    {
        _rpc = rpc;
    }

    public async Task Consume(ConsumeContext<StepBRequest> ctx)
    {
        var response = await _rpc.CallAsync<StepCRequest, StepCResponse>(
            new StepCRequest($"{ctx.Message.Value}-b", ctx.Message.CorrelationId),
            ctx.CancellationToken);

        await ctx.RespondAsync(new StepBResponse(response.Value));
    }
}

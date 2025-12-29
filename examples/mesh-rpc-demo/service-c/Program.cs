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

await MeshServiceHost.RunAsync(
    app =>
    {
        app.MapGet("/health", () => Results.Ok("ok"));
    },
    options =>
    {
        options.ServiceName = "mesh-rpc-c";
        options.Port = 8083;
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
            x.AddConsumer<StepCConsumer>();
        });
    });

public sealed class StepCConsumer : IConsumer<StepCRequest>
{
    public Task Consume(ConsumeContext<StepCRequest> ctx) =>
        ctx.RespondAsync(new StepCResponse($"{ctx.Message.Value}-c"));
}

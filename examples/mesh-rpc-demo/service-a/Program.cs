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

        app.MapGet("/start", async (string value, IMeshRpc rpc, CancellationToken ct) =>
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var response = await rpc.CallAsync<StepBRequest, StepBResponse>(
                new StepBRequest(value, correlationId),
                ct);

            return Results.Ok(new { input = value, output = response.Value, correlationId });
        });
    },
    options =>
    {
        options.ServiceName = "mesh-rpc-a";
        options.Port = 8081;
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
            x.AddRequestClient<StepBRequest>();
        });

        services.AddMeshRpc();
    });

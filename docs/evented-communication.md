# Evented Service Communication (RabbitMQ or AWS SNS/SQS + MassTransit)

## Pattern
- **Contract:** Define/extend event DTOs in `src/Shared/ToskaMesh.Common/Messaging/MessageContracts.cs` (e.g., `UserCreated { UserId, Email, Timestamp }`).
- **Transport:** Set `Messaging:Transport` to `RabbitMq` (default) or `AwsSqs`. RabbitMQ uses `Messaging:RabbitMq*` settings; AWS uses `Messaging:AwsSqs:*` for region/scope/optional service URL and AWS credentials from the default provider chain.
- **Publisher (Service A):** Inject `IPublishEndpoint`; on the business event call `await _publishEndpoint.Publish(new UserCreated { … }, ct);`. Broker connection comes from the configured transport.
- **Consumer (Service B):** Implement `IConsumer<UserCreated>`; register consumer with MassTransit (`AddConsumer<>`, `ReceiveEndpoint("service-b-user-created", …)`); handle the event and optionally publish follow-on events.
- **Security:** RabbitMQ uses broker auth/TLS; AWS relies on IAM roles/credentials (or access key/secret) and optional `ServiceUrl` for LocalStack.
- **Telemetry:** OpenTelemetry + Prometheus already wired; add custom counters/histograms via `IMetricsRegistry` in consumers if desired.
- **Deployment:** Queues/topics are auto-created by MassTransit on first consumer. No chart changes beyond transport settings.

## Mesh RPC helper (request/response)
For simple service-to-service calls over the configured broker (RabbitMQ or AWS SNS/SQS), use `IMeshRpc` from `ToskaMesh.Common.Messaging`. It wraps MassTransit request/response so handlers look like synchronous calls.

Example flow: Service A receives HTTP, calls Service B; Service B calls Service C; Service C responds; the response bubbles back to A.

Contracts:
```csharp
public record StepBRequest(string Value, string CorrelationId);
public record StepBResponse(string Value);
public record StepCRequest(string Value, string CorrelationId);
public record StepCResponse(string Value);
```

Service A (HTTP -> B):
```csharp
app.MapPost("/start", async (string input, IMeshRpc rpc, CancellationToken ct) =>
{
    var result = await rpc.CallAsync<StepBRequest, StepBResponse>(
        new StepBRequest(input, Guid.NewGuid().ToString("N")), ct);

    return Results.Ok(result.Value);
});
```

Service B (consume -> call C -> respond):
```csharp
public sealed class StepBConsumer : IConsumer<StepBRequest>
{
    private readonly IMeshRpc _rpc;

    public StepBConsumer(IMeshRpc rpc)
    {
        _rpc = rpc;
    }

    public async Task Consume(ConsumeContext<StepBRequest> ctx)
    {
        var c = await _rpc.CallAsync<StepCRequest, StepCResponse>(
            new StepCRequest($"{ctx.Message.Value}-b", ctx.Message.CorrelationId),
            ctx.CancellationToken);

        await ctx.RespondAsync(new StepBResponse(c.Value));
    }
}
```

Service C (consume -> respond):
```csharp
public sealed class StepCConsumer : IConsumer<StepCRequest>
{
    public Task Consume(ConsumeContext<StepCRequest> ctx) =>
        ctx.RespondAsync(new StepCResponse($"{ctx.Message.Value}-c"));
}
```

Registration (each service):
```csharp
services.AddMeshMassTransit(configuration, x =>
{
    x.AddRequestClient<StepBRequest>();
    x.AddRequestClient<StepCRequest>();
    x.AddConsumer<StepBConsumer>(); // only in service B
    x.AddConsumer<StepCConsumer>(); // only in service C
});
services.AddMeshRpc();
```

## Transport configuration
- `RabbitMq`: defaults to localhost guest credentials; override via `Messaging:RabbitMqHost`, `RabbitMqVirtualHost`, `RabbitMqUsername`, and `RabbitMqPassword` (or matching environment variables).
- `AwsSqs`: set `Messaging:Transport` to `AwsSqs` and provide region plus optional scope/service URL for LocalStack. Credentials can come from IAM roles or the standard AWS env vars.

```json
{
  "Messaging": {
    "Transport": "AwsSqs",
    "AwsSqs": {
      "Region": "us-east-1",
      "Scope": "dev",
      "ServiceUrl": "http://localhost:4566"
    }
  }
}
```

## Flow Diagram
```
+-------------+        publish UserCreated        +-----------------------------+        consume        +----------------+
| Service A   |  --------------------------------> | RabbitMQ or SNS topic/SQS  |  -------------------> |   Service B    |
| (Publisher) |   (IPublishEndpoint.Publish)       |  exchange/topic + queue    |   (IConsumer<T>)     | (Consumer)     |
+-------------+                                    +-----------------------------+                      +----------------+
        |                                                                                                  |
        | emit follow-on events if needed                                                                 |
        v                                                                                                  v
   other services (any subscriber with matching contract)                                      downstream actions / replies
```

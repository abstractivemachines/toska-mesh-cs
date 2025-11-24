# Evented Service Communication (RabbitMQ + MassTransit)

## Pattern
- **Contract:** Define/extend event DTOs in `src/Shared/ToskaMesh.Common/Messaging/MessageContracts.cs` (e.g., `UserCreated { UserId, Email, Timestamp }`).
- **Publisher (Service A):** Inject `IPublishEndpoint`; on the business event call `await _publishEndpoint.Publish(new UserCreated { … }, ct);`. RabbitMQ connection comes from `externalServices.rabbitmq` (Helm values).
- **Consumer (Service B):** Implement `IConsumer<UserCreated>`; register consumer with MassTransit (`AddConsumer<>`, `ReceiveEndpoint("service-b-user-created", …)`); handle the event and optionally publish follow-on events.
- **Security:** Use RabbitMQ auth/TLS; service-to-service JWT/mTLS is for HTTP/gRPC, not needed for broker messages.
- **Telemetry:** OpenTelemetry + Prometheus already wired; add custom counters/histograms via `IMetricsRegistry` in consumers if desired.
- **Deployment:** Queues are auto-created by MassTransit on first consumer. No chart changes beyond RabbitMQ settings.

## Flow Diagram
```
+-------------+        publish UserCreated        +----------------+        consume        +----------------+
| Service A   |  --------------------------------> |   RabbitMQ     |  -------------------> |   Service B    |
| (Publisher) |   (IPublishEndpoint.Publish)       |  exchange/queue|   (IConsumer<T>)     | (Consumer)     |
+-------------+                                    +----------------+                      +----------------+
        |                                                                                         |
        | emit follow-on events if needed                                                        |
        v                                                                                         v
   other services (any subscriber with matching contract)                              downstream actions / replies
```

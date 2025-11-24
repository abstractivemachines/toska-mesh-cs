# ToskaMesh Runtime SDK & Hosting Template (RuntimeV1)

## Goal
Give service authors a Lambda-like experience: add a NuGet package, call a single `AddMeshService()/UseMesh()` (stateless) or use a stateful Orleans template, and get registration, health, telemetry, auth, and routing metadata without hand-wiring infrastructure.

## Scope (RuntimeV1)
- **Stateless HTTP**: Minimal bootstrapping extension that wraps ASP.NET Core startup, injects Toska defaults (logging, telemetry, auth), and registers the service with discovery.
- **Stateful (Orleans)**: Template/extension that hosts grains with the same registration/health/telemetry pipeline.
- **Service registration + TTL heartbeat**: Auto-register on startup, keep Consul TTL checks alive, and deregister on shutdown.
- **Health + metadata**: Expose standard `/health` endpoints and attach routing metadata (scheme, health path, weights, lb strategy).
- **Telemetry/auth defaults**: Wire ToskaMesh.Telemetry and Mesh service auth with minimal config.
- **Config surface**: Single options object (env vars/appsettings) to describe service identity, ports, metadata, and discovery endpoints.

## Non-goals (RuntimeV1)
- Full multi-tenant hosting platform.
- Advanced deployment UX (CLI/operator) beyond providing hooks for later work.
- Custom business logic scaffolding (keep templates simple).

## Proposed API Surface
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMeshService("orders-api", options =>
{
    options.Port = 8080;
    options.HealthEndpoint = "/health";
    options.Metadata["scheme"] = "http";
    options.Metadata["weight"] = "2";
    options.Metadata["lb_strategy"] = "RoundRobin";
});

var app = builder.Build();
app.MapMeshDefaults(); // adds health endpoints, telemetry exporters, auth
app.MapControllers();
app.Run();
```

Stateful (Orleans) template:
```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseMeshSilo("inventory-silo", options =>
    {
        options.Port = 11111;
        options.GatewayPort = 30000;
        options.HealthEndpoint = "/health";
        options.Metadata["scheme"] = "http";
    })
    .ConfigureServices(services => services.AddMeshSiloDefaults());

await builder.RunConsoleAsync();
```

## Components
- **ToskaMesh.Runtime (new package)**: Extension methods + hosting helpers for stateless services.
- **ToskaMesh.Runtime.Orleans (new package)**: Silo builder extensions and registration glue for stateful workloads.
- **Heartbeat service**: Background service that renews Consul TTL checks with jitter; resilient to outages; graceful deregistration.
- **Registration pipeline**: On startup, register with discovery (initially Consul via GrpcServiceRegistry/ConsulServiceRegistry); attach metadata (scheme, health path, weights, lb strategy).
- **Health endpoints**: Map `/health`, `/health/ready`, `/health/live` and optionally a probe endpoint for custom checks.
- **Telemetry/auth defaults**: Call existing ToskaMesh.Telemetry + Mesh auth defaults; allow opt-out switches for minimal overhead.

## Configuration
- **Identity**: `Mesh:ServiceName`, `Mesh:ServiceId` (default to `<name>-<guid>`), `Mesh:Port`.
+- **Discovery**: `Mesh:ServiceDiscovery:Provider` (grpc/consul), addresses, tokens.
- **Health**: `Mesh:Health:Endpoint`, `Mesh:Health:IntervalSeconds`, `Mesh:Health:TimeoutSeconds`.
- **Metadata**: `Mesh:Metadata:*` for scheme/weights/lb_strategy/health_check_endpoint/tcp_port.
- **Telemetry/Auth**: reuse existing options (`Mesh:Telemetry`, `Mesh:ServiceAuth`).

## Behavior
1. **Startup**: configure logging/telemetry/auth → register with discovery → mark TTL as passing → start heartbeat background service.
2. **Runtime**: renew TTL periodically; update metadata if configured; surface metrics for registration/heartbeat outcomes.
3. **Shutdown**: stop heartbeats, deregister from discovery.

## Deliverables (RuntimeV1)
1) New projects: `ToskaMesh.Runtime` and `ToskaMesh.Runtime.Orleans` with public extension methods and options.
2) Heartbeat background service used by both runtime packages.
3) Default health mapping helper (`MapMeshDefaults`).
4) Sample stateless and stateful services consuming the SDK.
5) Docs: quickstart snippet, options table, and lifecycle description.
6) Tests: unit tests for options binding and heartbeat scheduling; integration smoke that registers + heartbeats + deregisters.

## Open Questions
- Should stateless template auto-enable MassTransit by default or keep opt-in?
- Do we enforce HTTPS-only by default for registration metadata?
- Where to plug router-aware hints (weights/strategies) so gateway consumes them consistently?

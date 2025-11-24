# MeshServiceHost Quickstart

## Stateless service (Lambda-style)

```csharp
using ToskaMesh.Runtime;

await MeshServiceHost.RunAsync(
    app =>
    {
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.Append("x-mesh", "yes");
            await next();
        });

        app.MapGet("/hello", () => Results.Ok(new { message = "hi" }));
    },
    options =>
    {
        options.ServiceName = "hello-api";
        options.Port = 8080;
        options.Metadata["lb_strategy"] = "RoundRobin";
    });
```

**What you get:**
- Routing via `MeshServiceApp` DSL (`MapGet/MapPost/...`).
- Telemetry/auth/health wired by default.
- Auto-registration + heartbeat (Consul/gRPC via `IServiceRegistry`).
- Custom middleware via `app.Use(...)` without exposing `WebApplication`.

## Stateful service (Orleans-backed)

```csharp
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Orleans;

await MeshServiceHost.RunStatefulAsync(
    configureSilo: silo =>
    {
        silo.ServiceName = "inventory-stateful";
        silo.ClusterId = "prod-cluster";
        silo.ClusteringMode = "consul";
        silo.ConsulAddress = "http://consul:8500";
    },
    configureOptions: options =>
    {
        options.ServiceName = "inventory-stateful";
        options.Metadata["scheme"] = "http";
        options.Metadata["health_check_endpoint"] = "/health";
    });
```

**What you get:**
- Orleans hosting without exposing silo details.
- Same registration/telemetry/auth/heartbeat pipeline as stateless hosts.

## Key options

| Option | Purpose |
| --- | --- |
| `ServiceName` / `ServiceId` | Logical name and instance id (id defaults to name + guid). |
| `Port` / `Address` | Bind address; set `Port = 0` for ephemeral ports in tests. |
| `HealthEndpoint`, `HealthInterval`, `HealthTimeout` | Health check path and heartbeat cadence. |
| `HeartbeatEnabled` | Toggle TTL renewal via `MeshHeartbeatService`. |
| `EnableTelemetry` / `EnableAuth` | Toggle mesh telemetry and auth defaults (set to false to disable). |
| `RegisterAutomatically` | Controls auto-registration on startup. |
| `AllowNoopServiceRegistry` | When true and no `IServiceRegistry` is registered, a no-op registry is used (tests/dev). Set false to fail fast. |
| `Metadata` | Routing hints (e.g., `scheme`, `health_check_endpoint`, `weight`, `lb_strategy`). |

## Notes
- If you don’t register an `IServiceRegistry`, the host will insert a no-op registry. For real deployments, set `AllowNoopServiceRegistry = false` or supply a registry.
- Middleware hook is available via `MeshServiceApp.Use(Func<HttpContext, Func<Task>, Task>)`.
- Stateful path currently assumes Orleans; the abstraction hides silo config behind `MeshStatefulOptions`.
- Telemetry/auth are enabled by default. To opt out (e.g., lightweight internal jobs), set `EnableTelemetry = false` and/or `EnableAuth = false` in `MeshServiceOptions`.

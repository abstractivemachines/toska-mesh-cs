# MeshServiceHost Quickstart

Related docs: [MeshServiceHost diagram](meshservicehost-diagram.md), [runtime SDK design notes](runtime-sdk-design.md), samples in [`examples/hello-mesh-service/README.md`](../examples/hello-mesh-service/README.md) and [`examples/todo-mesh-service/README.md`](../examples/todo-mesh-service/README.md).

Runnable sample (NuGet consumer): `examples/hello-mesh-service` packs `ToskaMesh.Runtime`, restores from `./artifacts/nuget`, and shows how to run alongside the mesh with Docker Compose.

## Key/value helper (Redis or ToskaStore)
- Stateless: `services.AddMeshKeyValueStore(configuration);` (optional `options => options.KeyPrefix = "my-svc:";` for Redis).
- Stateful silo pattern: use `StatefulMeshHost` for grains only; expose HTTP via a separate `MeshServiceHost` front-end that uses an Orleans client. Set `StatefulHostOptions.KeyValue.Enabled = true` to wire an `IKeyValueStore` with a default prefix of the service name.
- Provider selection: set `Mesh:KeyValue:Provider` to `Redis` (default) or `ToskaStore`.
- Redis config: `Mesh:KeyValue:Redis:ConnectionString` (and optional `KeyPrefix`, `Database`).
- ToskaStore config: `Mesh:KeyValue:ToskaStore:BaseUrl` plus optional `AuthToken`, `KeyPrefix`. `ListKeysAsync`/`ListAsync` use `/kv/keys` when available; set `EnableKeyIndex=true` to fall back on a local key index if the endpoint is unavailable.
- API: inject `IKeyValueStore` and call `SetAsync(key, value, ttl)`, `GetAsync<T>(key)`, `ListKeysAsync(prefix)`, `ListAsync<T>(prefix)`, `DeleteAsync(key)`.
- ToskaStore guide: [docs/toskastore.md](toskastore.md) and the
  [ToskaStore README](https://github.com/nullsync/toska_store/blob/main/README.md).

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
        options.Routing.Strategy = LoadBalancingStrategy.RoundRobin;
        options.Routing.Weight = 1;
    });
```

**What you get:**
- Routing via `MeshServiceApp` DSL (`MapGet/MapPost/...`).
- Telemetry/auth/health wired by default.
- Auto-registration + heartbeat (Consul/gRPC via `IServiceRegistry`).
- Custom middleware via `app.Use(...)` without exposing `WebApplication`.

## Stateful service (Orleans-backed, provider-agnostic surface)

```csharp
using ToskaMesh.Runtime;
using ToskaMesh.Runtime.Stateful;

await StatefulMeshHost.RunAsync(
    configureStateful: stateful =>
    {
        stateful.ServiceName = "inventory-stateful";
        stateful.Orleans.ClusterId = "prod-cluster";
        stateful.Orleans.ClusterProvider = StatefulClusterProvider.Consul;
        stateful.Orleans.ConsulAddress = "http://consul:8500";
    },
    configureService: options =>
    {
        options.ServiceName = "inventory-stateful";
        options.Routing.Scheme = "http";
        options.Routing.HealthCheckEndpoint = "/health";
    });
```

**What you get:**
- Orleans hosting by default without surfacing Orleans types (provider can be swapped later).
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
| `AllowNoopServiceRegistry` | Defaults to false. When true and no `IServiceRegistry` is registered, a no-op registry is used (tests/dev). In non-development environments, noop is rejected unless explicitly allowed. |
| `Metadata` | Routing hints (e.g., `scheme`, `health_check_endpoint`, `weight`, `lb_strategy`). |

## Notes
- If you don’t register an `IServiceRegistry`, the host will insert a no-op registry only when explicitly allowed (or in Development). For real deployments, leave `AllowNoopServiceRegistry` as false and supply a registry.
- Middleware hook is available via `MeshServiceApp.Use(Func<HttpContext, Func<Task>, Task>)`.
- Stateful path currently assumes Orleans under the hood but is exposed via `StatefulMeshHost` + `StatefulHostOptions` to keep implementation details out of consumer code.
- Telemetry/auth are enabled by default. To opt out (e.g., lightweight internal jobs), set `EnableTelemetry = false` and/or `EnableAuth = false` in `MeshServiceOptions`.

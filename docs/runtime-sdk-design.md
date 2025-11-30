See also: [MeshServiceHost quickstart](meshservicehost-quickstart.md) for usage examples, [MeshServiceHost diagram](meshservicehost-diagram.md), [Evented communication](evented-communication.md), and runtime package READMEs (`src/Shared/ToskaMesh.Runtime/PackageReadme.md`, `src/Shared/ToskaMesh.Runtime.Stateful/PackageReadme.md`, `src/Shared/ToskaMesh.Runtime.Orleans/PackageReadme.md`).

### Middleware hook
- `MeshServiceApp.Use(Func<HttpContext, Func<Task>, Task>)` allows adding custom middleware without exposing ASP.NET types directly.
- Use for cross-cutting concerns (headers, auth shims) before mapped routes.

### Heartbeat
- `MeshHeartbeatService` renews health status via `IServiceRegistry.UpdateHealthStatusAsync` on `HealthInterval`.
- Controlled by `MeshServiceOptions.HeartbeatEnabled`.

### Registry fallback
- If no `IServiceRegistry` is registered, a noop registry is added. This is intended for tests/dev only; set `AllowNoopServiceRegistry = false` to fail fast when discovery is misconfigured.

### Stateful hosting
- `StatefulMeshHost.RunAsync(...)` (in `ToskaMesh.Runtime.Stateful`) wraps the provider-specific host (Orleans by default) without exposing silo configuration. Uses `StatefulHostOptions` for cluster settings and `MeshServiceOptions` for registration/telemetry/auth/heartbeat.

### Auth/Telemetry defaults
- Defaults: telemetry and auth are enabled. Set `EnableTelemetry = false` and/or `EnableAuth = false` in `MeshServiceOptions` to opt out for lightweight services.
- For stateful hosts, the same options flow through `StatefulMeshHost` and are applied to the underlying host.

### Middleware hook
- `MeshServiceApp.Use(Func<HttpContext, Func<Task>, Task>)` allows adding custom middleware without exposing ASP.NET types directly.
- Use for cross-cutting concerns (headers, auth shims) before mapped routes.

### Heartbeat
- `MeshHeartbeatService` renews health status via `IServiceRegistry.UpdateHealthStatusAsync` on `HealthInterval`.
- Controlled by `MeshServiceOptions.HeartbeatEnabled`.

### Registry fallback
- If no `IServiceRegistry` is registered, a noop registry is added. This is intended for tests/dev only; set `AllowNoopServiceRegistry = false` to fail fast when discovery is misconfigured.

### Stateful hosting
- `MeshServiceHost.RunStatefulAsync(...)` wraps Orleans hosting without exposing silo configuration. Uses `MeshSiloOptions` for silo/cluster settings and `MeshServiceOptions` for registration/telemetry/auth/heartbeat.

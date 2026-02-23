# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Test, and Run

All commands run from this directory (`toska-mesh-cs/`).

```bash
dotnet restore ToskaMesh.sln
dotnet build ToskaMesh.sln -c Release
dotnet test ToskaMesh.sln
dotnet format ToskaMesh.sln

# Run a single test project
dotnet test tests/ToskaMesh.Security.Tests/ToskaMesh.Security.Tests.csproj

# Local infrastructure
docker-compose up -d postgres redis consul prometheus grafana rabbitmq
```

### Python CLI (`tools/cli/`)

```bash
cd tools/cli && make install   # venv + install
make format   # Black
make lint     # Ruff
make typecheck  # mypy
make test     # pytest
```

## Architecture

ToskaMesh is a distributed service mesh. The **control plane** (Gateway, Discovery, HealthMonitor, Router) has been rewritten in Go and lives in `../toska-mesh/`. This C# repository now contains the **runtime SDK** and **business services** that join the mesh.

External traffic enters through the Go **Gateway** (port 5000), which dynamically builds routes from service metadata in Go **Discovery** (gRPC registry backed by Consul, port 8080). C# services use the **ToskaMesh.Runtime** NuGet library to auto-register, wire telemetry, and expose health endpoints. **MassTransit** provides pub/sub and RPC messaging over RabbitMQ or AWS SNS/SQS.

### Key Interaction Flow

1. Service starts via `MeshLambdaService.RunAsync()` → `MeshAutoRegistrar` registers with Discovery/Consul
2. `MeshHeartbeatService` renews TTL health checks at configured intervals
3. Go Gateway polls Consul every 30s, builds routes from healthy instances
4. Routes are `/{RoutePrefix}/{serviceName}/{**catch-all}` (default prefix: `/api/`)
5. Load balancing strategy, weight, scheme, and health endpoint are driven by service metadata
6. Go HealthMonitor continuously probes services with circuit breakers
7. Health transitions publish `ServiceHealthChangedEvent` via RabbitMQ; unhealthy instances are filtered from Gateway routes

### Service Registry Abstraction

`IServiceRegistry` (in `ToskaMesh.Protocols`) is the central abstraction. Two implementations:
- `ConsulServiceRegistry` — direct Consul via `IConsulClient`, TTL-based health checks
- `GrpcServiceRegistry` — calls Discovery's gRPC service (`discovery.proto`: Register, Deregister, GetInstances, GetServices, ReportHealth)

Selected via `AddMeshInfrastructure()` with `ServiceRegistryProvider.Consul` (default) or `ServiceRegistryProvider.Grpc`.

### Runtime Library Pattern (`MeshLambdaService`)

External services consume the mesh through this high-level hosting API:

```csharp
await MeshLambdaService.RunAsync(
    app => { app.MapGet("/hello", () => Results.Ok(new { message = "hi" })); },
    options => { options.ServiceName = "my-service"; options.Port = 8080; },
    services => { services.AddGrpcServiceRegistry(config); }
);
```

- `MeshServiceApp` wraps `WebApplication`, exposing only Map*/Use() methods
- `MeshLambdaServiceHandle` (from `StartAsync`) provides `HttpClient` + DI for testing
- Key options: `ServiceName`, `Port` (0 = ephemeral), `Address`, `AdvertisedAddress`, `HealthEndpoint` ("/health"), `Metadata`, `RegisterAutomatically`, `AllowNoopServiceRegistry`
- Metadata propagation: `scheme`, `health_check_endpoint`, `lb_strategy`, `weight` stored in Consul/Discovery for Gateway routing decisions

### Infrastructure Wiring

Each service calls `AddMeshInfrastructure()` with toggleable features:

```csharp
builder.Services.AddMeshInfrastructure(builder.Configuration, options => {
    options.EnableMassTransit = false;
    options.EnableRedisCache = false;
    options.ConfigureDatabase = (s, c) => s.AddPostgres<AuthDbContext>(c);
});
```

### Configuration Sections

- `Mesh:Telemetry` — `MeshTelemetryOptions`
- `Mesh:ServiceAuth` — `MeshServiceAuthOptions` (service-to-service JWT)
- `Jwt` — `JwtTokenOptions` (Auth/Config services)

### Resilience

The Go Gateway implements retry with exponential backoff + jitter and per-service circuit breakers. See `../toska-mesh/CLAUDE.md` for details.

## Project Structure

- **`src/Services/`** — AuthService (JWT/Identity), ConfigService (centralized config/YAML), MetricsService (Prometheus aggregation), TracingService (Jaeger/Zipkin), ObservabilityService (portal with topology/SLOs)
- **`src/Shared/`** — Runtime (MeshLambdaService), Runtime.Stateful/Orleans, Common (Consul/gRPC registries, MassTransit, messaging contracts), Grpc (protobuf), Protocols (IServiceRegistry), Security (JWT/auth policies), Telemetry (OpenTelemetry)
- **`tests/`** — Mirrors production namespaces with `.Tests` suffix
- **`examples/`** — hello-mesh-service, todo-mesh-service, adder-mesh-service, profile-kv-store-demo, redis-grain-storage-demo
- **`tools/cli/`** — Python Toska CLI (init, validate, build, push, deploy)
- **`deployments/`** — Docker Compose, Dockerfiles, Prometheus/Grafana configs

## Coding Conventions

- .NET 10 / C# 14, nullable enabled, implicit usings enabled (`Directory.Build.props`).
- Central NuGet version management via `Directory.Packages.props` — all `.csproj` use version-less `<PackageReference>`. MassTransit 8.5.x, Orleans 9.2.x, and OpenTelemetry 1.14.x packages must use matching minor versions within their groups.
- 4-space indentation. `PascalCase` types/methods, `camelCase` locals/fields, `I`-prefix interfaces, `Async` suffix on async methods.
- Constructor injection with options pattern. Pass `CancellationToken` as last parameter on async entry points.
- Mirror existing patterns before introducing new ones. Treat warnings as actionable.

## Testing Conventions

- xUnit + FluentAssertions. Test naming: `Method_Scenario_ExpectedResult`. `[Fact]` for single cases, `[Theory]` for parameterized.
- In-memory hosting: `MeshLambdaService.StartAsync()` returns `MeshLambdaServiceHandle` with `HttpClient` for integration tests. Use `Port = 0` for ephemeral ports.
- Custom `RecordingServiceRegistry` available for verifying registration calls without real Consul.
- Prefer `Microsoft.AspNetCore.TestHost` for HTTP flow tests.

## Configuration & Secrets

- Never commit secrets. Use `.env` or shell exports for local dev.
- Required env vars: `MESH_SERVICE_AUTH_SECRET` (32+ chars), `MESH_SERVICE_AUTH_ISSUER`, `MESH_SERVICE_AUTH_AUDIENCE`.
- Service ports: `Mesh__Gateway__Port`, `Mesh__ServiceDiscovery__Port`, etc.
- Discovery gRPC address: `Mesh__ServiceDiscovery__Grpc__Address=http://localhost:8080`.

## Commit Style

Short, imperative messages (e.g., "Add JWT token refresh endpoint"). One feature/fix per commit. PRs should summarize intent, list testing performed, and call out config/env/port changes.

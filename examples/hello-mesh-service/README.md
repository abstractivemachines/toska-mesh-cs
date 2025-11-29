# Hello Mesh Service (runtime consumer example)

This sample shows how an end-user would consume the `ToskaMesh.Runtime` NuGet package to build a stateless HTTP service, register with the mesh, and run it locally or in Docker next to the ToskaMesh control plane.

## Prerequisites
- .NET 8 SDK
- Local packages built from this repo (NuGet feed at `./artifacts/nuget`)
- Optional: Docker + Docker Compose for running with the control plane

## 1) Build the runtime packages
Pack the runtime so the example can restore from the local feed (`examples/hello-mesh-service/NuGet.config` points at `../../artifacts/nuget`):

```bash
dotnet pack src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ./artifacts/nuget
```

## 2) Run the sample locally
The appsettings default to discovery on `http://discovery:80` (Docker network). Override for local runs:

```bash
export Mesh__ServiceDiscovery__Grpc__Address=http://localhost:15010
export Mesh__ServiceDiscovery__Grpc__AllowInsecureTransport=true
export Mesh__ServiceAuth__Secret=local-dev-mesh-service-secret-32chars

dotnet restore examples/hello-mesh-service/HelloMeshService.csproj --configfile examples/hello-mesh-service/NuGet.config
dotnet run --project examples/hello-mesh-service/HelloMeshService.csproj
```

Key endpoints:
- `GET /health` (via mesh defaults)
- `GET /metrics` (Prometheus scraping)
- `GET /hello` (sample payload)
- `GET /todos`, `GET /todos/all`, `GET /todos/{id}`, `POST /todos` (in-memory store)

## 3) Run alongside the ToskaMesh runtime with Docker Compose
Start the control plane + infra (from repo root):

```bash
docker compose -f deployments/docker-compose.yml up -d consul postgres redis rabbitmq prometheus grafana discovery gateway
```

Build and run the sample service on the same network:

```bash
docker build -f examples/hello-mesh-service/Dockerfile -t hello-mesh-service:local .
docker compose -f deployments/docker-compose.yml -f examples/hello-mesh-service/docker-compose.override.yml up -d hello-mesh-service
```

Verify:
- Service health: `curl http://localhost:18080/health`
- Through gateway once routes refresh (default prefix `/api/`): `curl http://localhost:15000/api/hello-mesh-service/hello`
- Check registration in Consul UI: `http://localhost:8500`

### Notes
- `Mesh:ServiceAuth:Secret` must be 32+ chars; override for non-dev environments.
- `Mesh:ServiceDiscovery:Grpc:AllowInsecureTransport` is only for local dev (`http`); use TLS in production.
- The Dockerfile copies `./artifacts/nuget` into the build context; rebuild packages before rebuilding the image after runtime changes.

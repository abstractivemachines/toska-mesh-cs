# Getting Started

Single-page quickstart for running Toska Mesh locally.

## Local demo with Docker Compose
- Prereqs: Docker, .NET 8 SDK (for building/running services).
- Set a 32+ char mesh service secret to avoid HS256 key length errors during gateway route refresh:
```bash
export MESH_SERVICE_AUTH_SECRET="local-dev-mesh-service-secret-32chars"
export MESH_SERVICE_AUTH_ISSUER="ToskaMesh.Services"
export MESH_SERVICE_AUTH_AUDIENCE="ToskaMesh.Services"
```
- From `deployments/`, bring up infra plus gateway/discovery:
```bash
cd deployments
docker-compose up -d postgres redis consul prometheus grafana gateway discovery
```
- Useful URLs: Consul `http://localhost:8500`, Gateway `http://localhost:5000`, Discovery `http://localhost:5010`, Prometheus `http://localhost:9090`, Grafana `http://localhost:3000`.

## Run services from source against Docker infra
- Start infra only: `cd deployments && docker-compose up -d postgres redis consul prometheus grafana`.
- Use helper scripts from repo root (`./run-gateway.sh`, `./run-discovery.sh`) or run directly:
```bash
cd src/Core/ToskaMesh.Gateway && dotnet run
cd src/Core/ToskaMesh.Discovery && dotnet run
```
- Add other services in additional terminals as needed.

## Quick smoke checks
- Health: `curl http://localhost:5000/health` (gateway) and `curl http://localhost:5010/health` (discovery).
- Metrics: `curl http://localhost:5000/metrics` and `curl http://localhost:5010/metrics`.
- Registration round-trip: register a dummy service with discovery, then confirm it shows under Consul Services in the UI.

## Next docs
- Runtime hosting: [MeshServiceHost quickstart](meshservicehost-quickstart.md) with stateless/stateful samples.
- Deployments: [Talos quickstart](../deployments/QUICKSTART-TALOS.md) and [EKS quickstart](../deployments/QUICKSTART-EKS.md); Terraform under `deployments/terraform/eks/README.md`.
- Examples: [examples/README.md](../examples/README.md) for runnable samples and per-example guides.

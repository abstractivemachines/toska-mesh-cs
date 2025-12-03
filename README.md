# Toska Mesh - C# Implementation

Distributed service mesh for .NET 8 with gateway, discovery, and runtime libraries (ported from the original Elixir implementation).

## Quick start
- Full quickstart: [docs/GETTING_STARTED.md](docs/GETTING_STARTED.md).
- Fast path (Docker Compose):
```bash
export MESH_SERVICE_AUTH_SECRET="local-dev-mesh-service-secret-32chars"
export MESH_SERVICE_AUTH_ISSUER="ToskaMesh.Services"
export MESH_SERVICE_AUTH_AUDIENCE="ToskaMesh.Services"
cd deployments
docker-compose up -d postgres redis consul prometheus grafana gateway discovery
```
- Health checks: `curl http://localhost:5000/health` (gateway) and `curl http://localhost:5010/health` (discovery). Consul UI at `http://localhost:8500`.

## Documentation
- Docs index: [docs/README.md](docs/README.md) for architecture, operations, deployments, and plans.
- Runtime hosting: [docs/meshservicehost-quickstart.md](docs/meshservicehost-quickstart.md); samples under `examples/`.
- Decisions and history: ADRs in [docs/adr/README.md](docs/adr/README.md); changelog index in [docs/CHANGELOG.md](docs/CHANGELOG.md) with entries in `changes/`.

## Repository layout
```
src/            # Core, services, and shared libraries
tests/          # Unit/integration tests
deployments/    # Docker Compose, Helm, Terraform, quickstarts
examples/       # Runnable samples (stateless/stateful)
docs/           # Guides, ADRs, plans, changelog
tools/          # CLI helper
```

## Common commands
- Restore/build/test: `dotnet restore ToskaMesh.sln`, `dotnet build ToskaMesh.sln -c Release`, `dotnet test ToskaMesh.sln`.
- Run gateway/discovery from source: `./run-gateway.sh`, `./run-discovery.sh` (or `dotnet run` in the respective project directories).
- Formatting: `dotnet format` (respect solution settings).

## Security & configuration
- Keep secrets (JWT, connection strings, TLS material) out of source control; prefer `.env` or shell exports when using Docker Compose.
- Set `MESH_SERVICE_AUTH_SECRET` to a strong 32+ character value before running gateway/discovery; align issuer/audience across services.
- Ports and endpoints can be overridden via environment variables defined in `deployments/docker-compose.yml`.
- [ ] Production documentation

## Contributing

1. Follow C# coding conventions
2. Add XML documentation comments
3. Write unit tests for new features
4. Update this README for significant changes
5. Use `dotnet format` before committing

## License

Licensed under the Apache License 2.0. See `LICENSE` and `NOTICE` for details.

## Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core/)
- [Orleans Documentation](https://docs.microsoft.com/dotnet/orleans/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Polly Documentation](https://github.com/App-vNext/Polly)
- `MeshServiceHost` quickstart: `docs/meshservicehost-quickstart.md`
- Runnable example service (NuGet consumer): `examples/hello-mesh-service`
- Runtime packaging: `docs/runtime-packaging.md`

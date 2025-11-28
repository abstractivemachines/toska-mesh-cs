# Repository Guidelines

## Project Structure & Module Organization
Source lives under `src/`: `Core` (gateway, discovery, router, health monitor), `Services` (auth, config, metrics, tracing), and `Shared` (utilities, security, telemetry, runtime). Tests sit in `tests/*` with `.Tests` projects per service. Operational assets live in `deployments` (docker-compose), `helm`, and `k8s`; docs in `docs`. Use `ToskaMesh.sln` as the entrypoint; helper scripts `run-gateway.sh` and `run-discovery.sh` start key services locally.

## Build, Test, and Development Commands
- `dotnet restore ToskaMesh.sln` – restore all packages (centralized via `Directory.Packages.props`).
- `dotnet build ToskaMesh.sln -c Release` – validate the solution; CI builds individual projects in Release.
- `dotnet test ToskaMesh.sln` – run the full test suite; scope to a project (e.g., `tests/ToskaMesh.Security.Tests/ToskaMesh.Security.Tests.csproj`) for quicker feedback.
- `cd deployments && docker-compose up -d postgres redis consul prometheus grafana` – start local infra; add `gateway discovery authservice configservice` to run the mesh.
- Run a service with `dotnet run` from its project directory (e.g., `src/Core/ToskaMesh.Gateway`).

## Coding Style & Naming Conventions
Target .NET 8/C# 12 with `Nullable` and implicit usings enabled. Prefer 4-space indentation, `PascalCase` for types/methods, `camelCase` for locals/fields, `I`-prefixed interfaces, and `Async` suffix for async methods. Keep dependencies centralized; prefer DI/options and pass cancellation tokens to entrypoints. Treat warnings as actionable and mirror existing patterns before introducing new ones.

## Testing Guidelines
xUnit with FluentAssertions is the default; tests mirror production namespaces and use the `.Tests` suffix. Name tests by behavior (`Method_Scenario_ExpectedResult`). Use `Fact` for single cases and `Theory` for data-driven coverage. Prefer in-memory/test host for HTTP flows; document required containers when adding integration tests. Add tests with new features and edge cases before refactors.

## Commit & Pull Request Guidelines
Recent history uses short, imperative commits (e.g., “Harden security for proxy”). Follow that style, keep scope focused, and group related changes. PRs should summarize intent, link issues, list testing performed (`dotnet test`, docker-compose smoke, etc.), and call out config/env or port changes. Update docs/config samples with code; add screenshots only when altering observable responses or dashboards.

## Security & Configuration Tips
Do not commit secrets; use `.env` or shell exports when running Compose. Set strong `MESH_SERVICE_AUTH_SECRET` (32+ chars) plus aligned issuer/audience before starting gateway/discovery. Override ports via `*_PORT` environment variables in `deployments/docker-compose.yml`. Keep TLS, JWT, and connection strings outside source control and rotate shared keys regularly.

# Example Consumer on ToskaMesh – Plan

Purpose: create an end-user sample that consumes the packaged ToskaMesh runtime and deploys to a running mesh cluster without referencing source code.

## Steps
- Package runtime: version bump if needed, `dotnet pack` `ToskaMesh.Runtime` and `ToskaMesh.Runtime.Stateful` (Orleans provider packaged separately), push to the chosen feed (NuGet.org or GitHub Packages) and validate restore.
- Scaffold consumer repo: minimal solution using package references only, `MeshServiceHost.RunAsync` with `/hello` endpoint and middleware example; keep in `examples/hello-mesh-service` or a separate repo.
- Configuration templates: `appsettings.json` with `Mesh:Service` (name/id/address/port/metadata), `Mesh:ServiceDiscovery` (gRPC or Consul), `Mesh:ServiceAuth` (32+ char secret, issuer/audience), `Mesh:Identity` roles; document env-var overrides for containers/K8s.
- Container + deployment: multi-stage `Dockerfile` (sdk 8.0 → aspnet 8.0), `docker-compose.override.yml` to run alongside the mesh stack, and a K8s/Helm snippet (Deployment + Service) pointing at Discovery gRPC and the mesh auth secret; optional ingress rule via Gateway.
- Runbook: start mesh infra (`docker-compose up -d ... gateway discovery` or Helm), set mesh auth secret + discovery address, run `dotnet run` to confirm registration (Consul/Discovery UI), then container/K8s deploy; verify `/health`, `/hello` through Gateway, and `/metrics` for telemetry.
- Optional: add a stateful variant using the stateful runtime package once the namespace surface is finalized; add a service-to-service call sample to show JWT and registry usage.

# Stateful Runtime Namespace Plan

Goal: hide Orleans implementation details behind a provider-agnostic stateful runtime surface for consumers.

## Approach
- Public surface: introduce `ToskaMesh.Runtime.Stateful` with `StatefulMeshHost.RunAsync(...)` and provider-neutral options; keep Orleans as the default provider behind nested provider options or an internal adapter.
- Packaging: ship a new `ToskaMesh.Runtime.Stateful` NuGet that depends on the Orleans package internally; move Orleans-specific types to internal or `.Internal` namespace; if assembly rename is required, publish a shim for one release.
- Docs/samples: update `docs/meshservicehost-quickstart.md` to reference `ToskaMesh.Runtime.Stateful`; add a stateful sample under `examples/` that uses only the provider-agnostic API; keep stateless sample unchanged.
- Deployment/config: keep config keys provider-neutral (`Mesh:Stateful:Provider=Orleans`, `Mesh:ServiceDiscovery:Grpc:Address=...`, mesh service auth secret); ensure samples work against existing Discovery gRPC and mesh auth flow.
- Rollout options: 
  1) New `ToskaMesh.Runtime.Stateful` package that wraps Orleans and hides it. 
  2) Keep package name, change public namespace to `ToskaMesh.Runtime.Stateful`, make Orleans types internal. 
  3) Ship stateless sample now, refactor stateful surface later. 
  Choose option, implement adapter, repack/publish, then update docs/samples.

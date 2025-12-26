# Examples

See [docs/README.md](../docs/README.md) for a full docs index and [MeshServiceHost quickstart](../docs/meshservicehost-quickstart.md) for runtime usage.

- `hello-mesh-service` – runnable stateless sample that consumes the `ToskaMesh.Runtime` NuGet package, registers with discovery, and can be deployed with the provided Dockerfile/compose override. See `examples/hello-mesh-service/README.md` for setup instructions.
- `adder-mesh-service` – minimal stateless sample that uses the `MeshService` base class API and exposes a simple `/add` endpoint. See `examples/adder-mesh-service/README.md` for usage.
- `todo-mesh-service` – stateful Orleans silo + HTTP API front-end. State persists in Redis via `IKeyValueStore`, clustering via Consul. See `examples/todo-mesh-service/README.md` (includes Kubernetes/Talos deployment notes).
- `profile-kv-store-demo` – simple profile API that persists data through `IKeyValueStore` backed by ToskaStore. See `examples/profile-kv-store-demo/README.md`.
- `redis-grain-storage-demo` – stateful Orleans silo using Redis as the grain storage provider plus a local HTTP API front-end. Uses local clustering (no Consul) and disables mesh registration for easy local testing. See `examples/redis-grain-storage-demo/README.md`.

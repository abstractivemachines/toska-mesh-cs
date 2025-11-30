# Examples

- `hello-mesh-service` – runnable stateless sample that consumes the `ToskaMesh.Runtime` NuGet package, registers with discovery, and can be deployed with the provided Dockerfile/compose override. See `examples/hello-mesh-service/README.md` for setup instructions.
- `todo-mesh-service` – stateful Orleans silo + HTTP API front-end. State persists in Redis via `IKeyValueStore`, clustering via Consul. See `examples/todo-mesh-service/README.md`.

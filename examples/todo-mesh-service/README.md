# Todo Mesh Service (stateful via Orleans + KV store)

Two processes:
- `todo-mesh-silo`: Orleans silo using `StatefulMeshHost`, clustering via Consul, state stored through `IKeyValueStore` (Redis by default).
- `todo-mesh-api`: HTTP API using `MeshServiceHost`, Orleans client calls grains in the silo.

Todos persist across restarts because state lives in the configured key/value store.

## Prerequisites
- .NET 8 SDK
- Local runtime packages in `./artifacts/nuget`
- Redis + Consul available (compose/Talos stacks already include both) or ToskaStore if configured
  (see the [ToskaStore README](https://github.com/abstractivemachines-com/toska_store/blob/main/README.md))

## Build + run locally
1) Pack runtime packages:
```bash
dotnet pack src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ./artifacts/nuget
dotnet pack src/Shared/ToskaMesh.Runtime.Stateful/ToskaMesh.Runtime.Stateful.csproj -c Release -o ./artifacts/nuget
```

2) Start infra (from repo root):
```bash
docker compose -f deployments/docker-compose.yml up -d consul redis discovery gateway
```

3) Start the silo:
```bash
dotnet restore examples/todo-mesh-service/silo/TodoMeshSilo.csproj --configfile examples/todo-mesh-service/NuGet.config
dotnet run --project examples/todo-mesh-service/silo/TodoMeshSilo.csproj
```

4) Start the API (separate terminal):
```bash
dotnet restore examples/todo-mesh-service/api/TodoMeshApi.csproj --configfile examples/todo-mesh-service/NuGet.config
dotnet run --project examples/todo-mesh-service/api/TodoMeshApi.csproj
```

HTTP endpoints (API host):
- `GET /todos/{id}`
- `POST /todos/{id}` body `{ "title": "...", "completed": false }`
- `PUT /todos/{id}` body `{ "title": "...", "completed": true/false }`
- `DELETE /todos/{id}`

## Docker Compose
Build and run both hosts alongside the mesh:
```bash
docker build -f examples/todo-mesh-service/Dockerfile.silo -t todo-mesh-silo:local .
docker build -f examples/todo-mesh-service/Dockerfile.api -t todo-mesh-api:local .
docker compose -f deployments/docker-compose.yml -f examples/todo-mesh-service/docker-compose.override.yml up -d todo-mesh-silo todo-mesh-api
```

Verify via gateway (default prefix `/api/`):
```bash
curl http://localhost:15000/api/todo-mesh-api/todos/abc -d '{"title":"hi","completed":false}' -H 'Content-Type: application/json' -X POST
curl http://localhost:15000/api/todo-mesh-api/todos/abc
```

Notes:
- Set `Mesh__KeyValue__Provider=Redis` (default) with `Mesh__KeyValue__Redis__ConnectionString` and optional `Database`/`KeyPrefix` for the silo; defaults match k8s overlays (`redis-master.toskamesh-infra.svc.cluster.local:6379`, DB 1, prefix `todo-mesh-silo:`).
- For ToskaStore, set `Mesh__KeyValue__Provider=ToskaStore` and `Mesh__KeyValue__ToskaStore__BaseUrl` (plus optional `AuthToken`, `KeyPrefix`).
- Orleans clustering uses Consul (`Consul:Address`); keep cluster/service ids aligned between silo and API (`mesh-stateful` / `todo-mesh-silo`).
- HTTP/2 plaintext is allowed for the Orleans client by setting `DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2UNENCRYPTEDSUPPORT=true` in local/dev.

## Using ToskaStore instead of Redis

1) Start ToskaStore (from the `toska_store` repo; see the
   [ToskaStore README](https://github.com/abstractivemachines-com/toska_store/blob/main/README.md)):
```bash
cd ~/src/toska_store/apps/toska
mix escript.build
./toska start --host 0.0.0.0 --port 4000
```

2) Run the silo with ToskaStore configuration:
```bash
export Mesh__KeyValue__Provider=ToskaStore
export Mesh__KeyValue__ToskaStore__BaseUrl=http://localhost:4000
export Mesh__KeyValue__ToskaStore__KeyPrefix=todo-mesh-silo:
```

Optional auth token:
```bash
export Mesh__KeyValue__ToskaStore__AuthToken=your-token
```

If the ToskaStore server does not expose `/kv/keys`, set:
```bash
export Mesh__KeyValue__ToskaStore__EnableKeyIndex=true
```

## Kubernetes / Talos deployment notes

- Overlays live in `k8s/todo-mesh-silo` and `k8s/todo-mesh-api`; they use Consul at `consul-server.toskamesh-infra.svc.cluster.local:8500`.
- Silo config: advertised IP from `status.podIP`, gateway port `30000`, service registration via Consul, Redis at `redis-master.toskamesh-infra.svc.cluster.local:6379` (DB 1, prefix `todo-mesh-silo:`). Probes are TCP on `11111`.
- API config: Orleans cluster/service IDs `mesh-stateful`/`todo-mesh-silo`, discovery gRPC `http://toskamesh-discovery.toskamesh.svc.cluster.local:50051`, mesh auth secret from `toskamesh-discovery-secrets`, readiness `/health/ready`.
- Images currently deployed: `192.168.50.73:5000/todo-mesh-silo:local` (`sha256:ca760705b8839fb8fdb8634a34fa77eb181918e135d5ca83c480170722aa3a33`), `192.168.50.73:5000/todo-mesh-api:local` (`sha256:58a6d149aba777dfbbcf45c49fb5780b395e1093d700b0fc9d1b439bac5f657b`).
- Validation: port-forward `svc/todo-mesh-api 18080:8080` and run `POST /todos/{id}` followed by `GET` to confirm API↔silo connectivity and Consul membership.

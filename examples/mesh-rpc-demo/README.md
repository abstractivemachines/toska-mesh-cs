# Mesh RPC demo (RabbitMQ + MassTransit)

Three mesh services chained via RabbitMQ request/response:
- `service-a` exposes HTTP and calls `service-b` using `IMeshRpc`.
- `service-b` handles the request, calls `service-c` using `IMeshRpc`, then responds.
- `service-c` handles the final request and responds.

## Prerequisites
- .NET 10 SDK
- RabbitMQ
- ToskaMesh CLI (`tools/cli`) for Kubernetes deployment
  (see `tools/cli/README.md` for setup)

## Run locally

1) Start RabbitMQ (from repo root):
```bash
RABBITMQ_PORT=5672 docker compose -f deployments/docker-compose.yml up -d rabbitmq
```
This maps RabbitMQ to `localhost:5672`, matching the default `Messaging:RabbitMqHost` in each service's `appsettings.json`.

2) Start the services (separate terminals):
```bash
dotnet run --project examples/mesh-rpc-demo/service-c/MeshRpcServiceC.csproj
dotnet run --project examples/mesh-rpc-demo/service-b/MeshRpcServiceB.csproj
dotnet run --project examples/mesh-rpc-demo/service-a/MeshRpcServiceA.csproj
```

3) Call the chain:
```bash
curl "http://localhost:8081/start?value=hello"
```
Expected response:
```json
{"input":"hello","output":"hello-b-c","correlationId":"..."}
```

## Deploy with Toska CLI (Kubernetes)

1) Validate the manifest:
```bash
toska validate -f examples/mesh-rpc-demo/toska.yaml
```

2) Build + push images to the local registry (update `registry` in `examples/mesh-rpc-demo/toska.yaml` if needed):
```bash
toska publish -f examples/mesh-rpc-demo/toska.yaml
```

3) Deploy (uses manifests in `k8s/mesh-rpc-demo`):
```bash
toska deploy -f examples/mesh-rpc-demo/toska.yaml
```

4) Port-forward service A and call the chain:
```bash
toska deploy -f examples/mesh-rpc-demo/toska.yaml --port-forward -w mesh-rpc-a
curl "http://localhost:8081/start?value=hello"
```

5) Check status (label selector is `app.kubernetes.io/component=example`):
```bash
toska status --namespace toskamesh-example --selector app.kubernetes.io/component=example
```

## RabbitMQ credentials (cluster)
The deployments read `Messaging__RabbitMqHost`/`Messaging__RabbitMqVirtualHost` from `k8s/mesh-rpc-demo/configmap.yaml`.
If your RabbitMQ uses non-default credentials, create a secret in the `toskamesh-example` namespace:
```bash
kubectl -n toskamesh-example create secret generic mesh-rpc-demo-rabbitmq \\
  --from-literal=Messaging__RabbitMqUsername=<user> \\
  --from-literal=Messaging__RabbitMqPassword=<password> \\
  --from-literal=Messaging__RabbitMqVirtualHost=/
```

## Notes
- These services use `MeshLambdaService` but set `AllowNoopServiceRegistry = true` and `RegisterAutomatically = false` for local runs.
- To point at a different RabbitMQ host, update `k8s/mesh-rpc-demo/configmap.yaml` and redeploy.

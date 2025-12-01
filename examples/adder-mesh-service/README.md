# Adder Mesh Service (base-class style)

Small stateless service that uses the `MeshService` base class instead of lambdas. It exposes a single endpoint that sums two numbers.

## Run locally

```bash
cd examples/adder-mesh-service
dotnet restore
dotnet run
```

Then call:

```bash
curl "http://localhost:8083/add?a=2&b=3"
# -> { "a":2,"b":3,"sum":5 }

curl -X POST "http://localhost:8083/add" \
  -H "Content-Type: application/json" \
  -d '{"a":4,"b":7}'
# -> { "a":4,"b":7,"sum":11 }
```

The sample defaults to registering with the gRPC discovery service at `http://discovery:80`; override via `Mesh:ServiceDiscovery:Grpc:Address` (see `appsettings.json`). Update `Mesh:ServiceAuth` for your environment before deploying beyond local/dev.

## Build and publish with Toska CLI

From this directory:

```bash
dotnet pack ../../src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ../../artifacts/nuget
toska publish --manifest toska.yaml
# Optionally port-forward after deploy:
toska deploy --port-forward --manifest toska.yaml
```

The manifest builds `192.168.50.73:5000/adder-mesh-service:local` using `examples/adder-mesh-service/Dockerfile` and applies the Kubernetes manifests under `k8s/adder-mesh-service`. Adjust registry/namespace in `toska.yaml` and the deployment manifest if your registry differs.

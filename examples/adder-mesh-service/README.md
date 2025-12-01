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

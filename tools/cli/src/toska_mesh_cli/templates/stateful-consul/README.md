# todo-mesh-service

Stateful mesh service template with an Orleans silo and a separate HTTP API.

## Run locally
Start the silo:
```bash
dotnet run --project silo/TodoMeshSilo.csproj
```

Start the API:
```bash
dotnet run --project api/TodoMeshApi.csproj
```

Override mesh settings as needed:
```bash
export Mesh__ServiceDiscovery__Grpc__Address=http://localhost:15010
export Mesh__ServiceDiscovery__Grpc__AllowInsecureTransport=true
export Mesh__ServiceAuth__Secret=local-dev-mesh-service-secret-32chars
```

## Build containers
```bash
docker build -f Dockerfile.silo -t todo-mesh-silo:local .
docker build -f Dockerfile.api -t todo-mesh-api:local .
```

## Deploy with Toska CLI
```bash
toska validate -f toska.yaml
toska deploy -f toska.yaml
```

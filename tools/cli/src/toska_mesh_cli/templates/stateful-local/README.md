# redis-grain-demo

Stateful mesh service template with a local Orleans cluster and Redis-backed grain storage.

## Run locally
Start the silo:
```bash
dotnet run --project silo/RedisGrainSilo.csproj
```

Start the API:
```bash
dotnet run --project api/RedisGrainApi.csproj
```

Override mesh settings as needed:
```bash
export Mesh__ServiceDiscovery__Grpc__Address=http://localhost:15010
export Mesh__ServiceDiscovery__Grpc__AllowInsecureTransport=true
export Mesh__ServiceAuth__Secret=local-dev-mesh-service-secret-32chars
```

## Build containers
```bash
docker build -f Dockerfile.silo -t redis-grain-silo:local .
docker build -f Dockerfile.api -t redis-grain-api:local .
```

## Deploy with Toska CLI
```bash
toska validate -f toska.yaml
toska deploy -f toska.yaml
```

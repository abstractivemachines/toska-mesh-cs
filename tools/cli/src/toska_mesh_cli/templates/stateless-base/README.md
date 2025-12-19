# adder-mesh-service

Stateless mesh service template using the `MeshService` base class.

## Run locally
```bash
dotnet run --project AdderMeshService.csproj
```

Override mesh settings as needed:
```bash
export Mesh__ServiceDiscovery__Grpc__Address=http://localhost:15010
export Mesh__ServiceDiscovery__Grpc__AllowInsecureTransport=true
export Mesh__ServiceAuth__Secret=local-dev-mesh-service-secret-32chars
```

## Build container
```bash
docker build -t adder-mesh-service:local .
```

## Deploy with Toska CLI
```bash
toska validate -f toska.yaml
toska deploy -f toska.yaml
```

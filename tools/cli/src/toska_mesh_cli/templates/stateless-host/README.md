# hello-mesh-service

Stateless mesh service template using `MeshServiceHost` (lambda-style routing).

## Run locally
```bash
dotnet run --project HelloMeshService.csproj
```

Override mesh settings as needed:
```bash
export Mesh__ServiceDiscovery__Grpc__Address=http://localhost:15010
export Mesh__ServiceDiscovery__Grpc__AllowInsecureTransport=true
export Mesh__ServiceAuth__Secret=local-dev-mesh-service-secret-32chars
```

## Build container
```bash
docker build -t hello-mesh-service:local .
```

## Deploy with Toska CLI
```bash
toska validate -f toska.yaml
toska deploy -f toska.yaml
```

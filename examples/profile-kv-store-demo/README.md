# Profile KV Store Demo (ToskaStore-backed)

Minimal profile API that stores profile data in `IKeyValueStore` using ToskaStore as the backing provider. It persists profiles across restarts and keeps the HTTP surface small for easy local testing.

Related guide: [docs/toskastore.md](../../docs/toskastore.md) and the
[ToskaStore README](https://github.com/nullsync/toska_store/blob/main/README.md).

## Prerequisites
- .NET 8 SDK
- Local runtime packages in `./artifacts/nuget`
- ToskaStore running locally (see below)

## 1) Build the runtime packages
Pack the runtime so the example can restore from the local feed (`NuGet.config` points at `../../artifacts/nuget`):

```bash
dotnet pack src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ./artifacts/nuget
```

## 2) Start ToskaStore
From the `toska_store` repo (see the
[ToskaStore README](https://github.com/nullsync/toska_store/blob/main/README.md)):

```bash
cd ~/src/toska_store/apps/toska
mix escript.build
./toska start --host 0.0.0.0 --port 4000
```

Kubernetes auth token secret (recommended):

```bash
kubectl -n toskamesh create secret generic profile-kv-store-demo-secrets \
  --from-literal=toskaStoreAuthToken=your-token
```

Then reference it in your deployment:

```yaml
- name: Mesh__KeyValue__ToskaStore__AuthToken
  valueFrom:
    secretKeyRef:
      name: profile-kv-store-demo-secrets
      key: toskaStoreAuthToken
```

## 3) Run the profile service

```bash
export Mesh__KeyValue__Provider=ToskaStore
export Mesh__KeyValue__ToskaStore__BaseUrl=http://localhost:4000
export Mesh__KeyValue__ToskaStore__KeyPrefix=profile-kv-store-demo

dotnet restore examples/profile-kv-store-demo/ProfileKvStoreDemo.csproj --configfile examples/profile-kv-store-demo/NuGet.config
dotnet run --project examples/profile-kv-store-demo/ProfileKvStoreDemo.csproj
```

If the ToskaStore server does not expose `/kv/keys`, set:

```bash
export Mesh__KeyValue__ToskaStore__EnableKeyIndex=true
```

## API

Create/update a profile:

```bash
curl -X PUT http://localhost:8084/profiles/alice \
  -H "Content-Type: application/json" \
  -d '{"displayName":"Alice","email":"alice@example.com","bio":"Platform engineer","tags":["orleans","kv"]}'
```

Read a profile:

```bash
curl http://localhost:8084/profiles/alice
```

List profiles:

```bash
curl http://localhost:8084/profiles?limit=25
```

Delete a profile:

```bash
curl -X DELETE http://localhost:8084/profiles/alice -i
```

Health check:

```bash
curl http://localhost:8084/health
```

## Code
- API + storage wiring: `examples/profile-kv-store-demo/Program.cs`
- Project config: `examples/profile-kv-store-demo/ProfileKvStoreDemo.csproj`
- Runtime config defaults: `examples/profile-kv-store-demo/appsettings.json`

## Kubernetes
The repository includes example manifests under `k8s/profile-kv-store-demo`.

Create the secret for ToskaStore auth (namespace optional):

```bash
kubectl -n toskamesh create secret generic profile-kv-store-demo-secrets \
  --from-literal=toskaStoreAuthToken=your-token
```

Update the image and ToskaStore base URL in `k8s/profile-kv-store-demo/deployment.yaml`, then apply:

```bash
kubectl -n toskamesh apply -f k8s/profile-kv-store-demo/service.yaml
kubectl -n toskamesh apply -f k8s/profile-kv-store-demo/deployment.yaml
```

## Toska CLI
Build and publish with Toska CLI (from this directory):

```bash
dotnet pack ../../src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ../../artifacts/nuget
toska publish --manifest toska.yaml
# Optional port-forward after deploy:
toska deploy --port-forward --manifest toska.yaml
```

The manifest builds `192.168.50.73:5000/profile-kv-store-demo:local` using `examples/profile-kv-store-demo/Dockerfile`. Update the registry or tag in `toska.yaml` and the deployment manifest if your registry differs.

Make sure the ToskaStore auth secret exists before deploying:

```bash
kubectl -n toskamesh create secret generic profile-kv-store-demo-secrets \
  --from-literal=toskaStoreAuthToken=your-token
```

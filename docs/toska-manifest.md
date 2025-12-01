# Toska manifest (toska.yaml)

The Toska CLI uses a `toska.yaml` manifest to know how to build and deploy a service. Place it in the project directory you want to publish (pass `--manifest` to override the path).

## Top-level keys

- `service`: logical service metadata.
  - `name`: mesh service name (matches what you register with discovery).
  - `type`: `stateless` or `stateful`.
- `deploy`: deployment target.
  - `target`: e.g., `kubernetes`.
  - `namespace`: Kubernetes namespace for the rendered manifests.
- `workloads`: array of workload definitions (typically one per container).
  - `name`: workload identifier.
  - `type`: `stateless` or `stateful`.
  - `manifests`: list of Kubernetes manifest paths (relative to the manifest file) to apply.
  - `image`: container image metadata.
    - `repository`: image name.
    - `tag`: tag to build/push.
    - `registry`: registry host (e.g., `localhost:5000` for local dev).
  - `build`: how to build the image.
    - `context`: docker build context.
    - `dockerfile`: path to the Dockerfile within the repo.

## Example

From `examples/adder-mesh-service/toska.yaml`:

```yaml
service:
  name: adder-mesh-service
  type: stateless

deploy:
  target: kubernetes
  namespace: toskamesh

workloads:
  - name: adder-mesh-service
    type: stateless
    manifests:
      - ../../k8s/adder-mesh-service/service.yaml
      - ../../k8s/adder-mesh-service/deployment.yaml
    image:
      repository: adder-mesh-service
      tag: local
      registry: localhost:5000
    build:
      context: ../..
      dockerfile: examples/adder-mesh-service/Dockerfile
```

## Publish flow (example)

From the manifest directory:

```bash
dotnet pack ../../src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ../../artifacts/nuget
toska publish --manifest toska.yaml
```

This builds and pushes `localhost:5000/adder-mesh-service:local` using the provided Dockerfile, then applies the Kubernetes manifests listed under `manifests`. Adjust `registry`, `namespace`, or manifest paths to match your environment.***

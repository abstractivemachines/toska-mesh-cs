# Toska Mesh CLI (Python)

This is the isolated Python command-line interface for the Toska Mesh repository. It is scaffolded as a standalone package under `tools/cli` so it can evolve independently from the .NET services.

## Layout
- `pyproject.toml` – packaging metadata and entry point declaration.
- `src/toska_mesh_cli/` – CLI package source.
- `tests/` – Python tests for the CLI package.
- `.gitignore` – Python-specific ignores scoped to this tool.

## Getting Started
```bash
cd tools/cli
python -m venv .venv
source .venv/bin/activate
pip install -e .[dev]

toska --help
toska info
toska validate -f ./toska.yaml
```

## Deploy (preview)
From a user service directory (e.g., under `examples/`), add a minimal `toska.yaml` and run:

```bash
toska deploy
```

Options:
```bash
toska deploy [-f ./toska.yaml] [--dry-run] [-v/--verbose] [--port-forward] [-w workload] [--kubeconfig ~/.kube/config] [--context my-cluster]
```
- Default manifest path: `toska.yaml` in the current directory.
- Supported target: Kubernetes (`kubectl` must be pointed at your cluster, ToskaMesh already running there).
- `--verbose` prints the underlying `kubectl` output; without it only the planned/executed commands are shown.
- `--port-forward` runs `kubectl port-forward` for workloads that declare `portForward` and keeps them alive until Ctrl+C.
- `-w/--workload` limits the deploy to specific workloads defined in the manifest.
- `--kubeconfig/--context` are forwarded to `kubectl` commands.

## Build / Push / Publish
Build images, push them to a registry, or do both (publish) based on image + build settings in `toska.yaml`:

```bash
toska build [-f ./toska.yaml] [--dry-run] [-v/--verbose] [-w workload]
toska push [-f ./toska.yaml] [--dry-run] [-v/--verbose] [-w workload]
toska publish [-f ./toska.yaml] [--dry-run] [-v/--verbose] [-w workload]  # build then push
```

Notes:
- Specify `workloads[*].image.repository|tag|registry` and `workloads[*].build.context|dockerfile`.
- `--dry-run` prints the planned docker commands; `-v` shows docker stdout/stderr.
- Commands emit progress by default; plans/commands are shown when using `--dry-run` or `-v`.
- `-w/--workload` scopes build/push/publish to specific workloads in the manifest.

## Validate
Validate a manifest and surface missing paths/fields:

```bash
toska validate [-f ./toska.yaml] [--json]
```
- Exit code is non-zero when validation errors are found.

## Status
Show deployments, services, and pods that match the selector/namespace:

```bash
toska status [--namespace toskamesh] [-l component=example] [--all] [--json] [--kubeconfig ~/.kube/config] [--context my-cluster]
```

## Services
List deployed Toska Mesh user services (defaults to namespace `toskamesh` and selector `component=example`):

```bash
toska services [--namespace toskamesh] [-l component=example] [--all] [--json] [--kubeconfig ~/.kube/config] [--context my-cluster]
```
- `--all` removes the label selector (may include core components).
- `--json` prints raw data for scripting.

## Deployments
List Toska Mesh user deployments (defaults to namespace `toskamesh` and selector `component=example`):

```bash
toska deployments [--namespace toskamesh] [-l component=example] [--all] [--json] [--kubeconfig ~/.kube/config] [--context my-cluster]
```
- `--all` removes the label selector (may include core components).
- `--json` prints raw data for scripting.

## Destroy
Delete resources described in the same manifest:

```bash
toska destroy [-f ./toska.yaml] [--dry-run] [-v/--verbose]
```

This issues `kubectl delete -f` for each manifest path listed in the plan.

Example manifest:
```yaml
service:
  name: hello-mesh-service
  type: stateless  # or stateful
deploy:
  target: kubernetes
  namespace: toskamesh
  manifests:
    - k8s/hello-mesh-service/deployment.yaml
image:
  repository: hello-mesh-service
  tag: local
  registry: localhost:5000
```

The deploy command currently validates the manifest and runs `kubectl apply` for each manifest (or prints the plan with `--dry-run`). ToskaMesh itself is assumed to already be deployed to the target environment.

## Notes
- Keep CLI logic and dependencies inside this folder to avoid leaking into the .NET solution.
- The CLI currently provides deploy/build/publish/destroy/status/validate commands; add orchestration or diagnostics features here without touching the .NET solution.
- Prefer adding dependencies to `pyproject.toml` with sensible version ranges and keep tests alongside features.

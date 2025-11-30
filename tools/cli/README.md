# Toska Mesh CLI (Python)

This is the isolated Python command-line interface for the Toska Mesh repository. It is scaffolded as a standalone package under `tools/cli` so it can evolve independently from the .NET services.

## Layout
- `pyproject.toml` – packaging metadata and entry point declaration.
- `src/toska_mesh_cli/` – CLI package source (placeholder today).
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
```

## Deploy (preview)
From a user service directory (e.g., under `examples/`), add a minimal `toska.yaml` and run:

```bash
toska deploy
```

Options:
```bash
toska deploy [-f ./toska.yaml] [--dry-run] [-v/--verbose]
```
- Default manifest path: `toska.yaml` in the current directory.
- Supported target: Kubernetes (`kubectl` must be pointed at your cluster, ToskaMesh already running there).
- `--verbose` prints the underlying `kubectl` output; without it only the planned/executed commands are shown.

## Build / Push / Publish
Build images, push them to a registry, or do both (publish) based on image + build settings in `toska.yaml`:

```bash
toska build [-f ./toska.yaml] [--dry-run] [-v/--verbose]
toska push [-f ./toska.yaml] [--dry-run] [-v/--verbose]
toska publish [-f ./toska.yaml] [--dry-run] [-v/--verbose]  # build then push
```

Notes:
- Specify `workloads[*].image.repository|tag|registry` and `workloads[*].build.context|dockerfile`.
- `--dry-run` prints the planned docker commands; `-v` shows docker stdout/stderr.
- Commands emit progress by default; plans/commands are shown when using `--dry-run` or `-v`.

## Services
List deployed Toska Mesh user services (defaults to namespace `toskamesh` and selector `component=example`):

```bash
toska services [--namespace toskamesh] [-l component=example] [--all] [--json]
```
- `--all` removes the label selector (may include core components).
- `--json` prints raw data for scripting.

## Deployments
List Toska Mesh user deployments (defaults to namespace `toskamesh` and selector `component=example`):

```bash
toska deployments [--namespace toskamesh] [-l component=example] [--all] [--json]
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
- The CLI currently provides placeholder commands only; we can add subcommands for orchestration, diagnostics, and developer workflows next.
- Prefer adding dependencies to `pyproject.toml` with sensible version ranges and keep tests alongside features.

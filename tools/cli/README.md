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

### Dev loop
```bash
# From tools/cli
make install   # create venv + install dev deps
make format    # black
make lint      # ruff
make typecheck # mypy (shallow)
make test      # pytest with coverage
```

## Local install (wheelhouse)
Package and install the CLI into `~/Applications` from a local wheelhouse directory:

```bash
# From tools/cli (ensure no venv is active)
./scripts/install-local.sh
```

Override the wheelhouse or install root:
```bash
./scripts/install-local.sh /path/to/local-packages ~/Applications/toska-mesh-cli
```

This writes a wrapper at `~/Applications/toska`. Add `~/Applications` to your PATH or run it directly.

## Init (scaffold)
Create a new service scaffold from built-in templates:

```bash
cd tools/cli
source .venv/bin/activate  # or create it via: python -m venv .venv

toska init inventory-service --type stateless --style host
toska init inventory-service --type stateless --style base
toska init inventory-service --type stateful --stateful-template consul
toska init inventory-service --type stateful --stateful-template local
```

Options:
- `-o/--output` target directory (defaults to `./<name>`).
- `--solution/--sln` adds generated `.csproj` files to an existing solution.
- `--runtime-version` overrides `ToskaMesh.Runtime` package version.
- `--orleans-version` overrides `Microsoft.Orleans.*` package version (stateful templates).
- `--force` overwrites files in an existing output directory.

## Kubeconfig (Talos)
Generate a Kubernetes kubeconfig using a Talos cluster (uses `talosctl` under the hood):
```bash
toska kubeconfig --talosconfig ./clusterconfig/talosconfig -e 192.168.50.229 --out ~/.kube/config --force
```
Options:
- `--talosconfig` path to the Talos client config (defaults to `clusterconfig/talosconfig`).
- `-e/--endpoint` Talos endpoint(s); if omitted, endpoints from the talosconfig are used.
- `--node` optional node(s) override; defaults to nodes from talosconfig when present.
- `-o/--out` output kubeconfig path (defaults to `~/.kube/config`); `--force` overwrites.
- `--discover-cidr` optional CIDR(s) to probe for Talos endpoints (opt-in). `--discover-port` (default `50000`), `--discover-timeout` (seconds, default `0.2`), and `--max-hosts` (default `256`) shape the scan.
- `-v/--verbose` streams `talosctl` output.

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
- `-n/--namespace` overrides the manifest namespace for apply/delete/port-forward.
- `--port-forward` runs `kubectl port-forward` for workloads that declare `portForward` and keeps them alive until Ctrl+C.
- `-w/--workload` limits the deploy to specific workloads defined in the manifest.
- `--kubeconfig/--context` are forwarded to `kubectl` commands.
- Progress output uses spinners and a summary when a TTY is detected; `-v` streams command output as it runs.

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
- Commands fail fast when `kubectl`/`docker` are missing (skipped when using `--dry-run`).

from __future__ import annotations

import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Optional, Sequence

import yaml

from .progress import ProgressReporter


class DeployConfigError(Exception):
    """Raised when the deploy manifest is invalid or a deployment fails."""


@dataclass(frozen=True)
class ImageRef:
    repository: str
    tag: str = "latest"
    registry: Optional[str] = None

    def as_string(self) -> str:
        prefix = f"{self.registry}/" if self.registry else ""
        return f"{prefix}{self.repository}:{self.tag}"


@dataclass(frozen=True)
class Workload:
    name: str
    mode: str
    manifests: List[Path]
    image: Optional[ImageRef] = None
    build_context: Optional[Path] = None
    dockerfile: Optional[Path] = None
    port_forward: Optional["PortForward"] = None


@dataclass(frozen=True)
class DeployConfig:
    service: str
    mode: str
    target: str
    namespace: Optional[str]
    workloads: List[Workload]


@dataclass(frozen=True)
class PortForward:
    service: str
    remote_port: int
    local_port: Optional[int] = None


@dataclass
class DeployOutcome:
    commands: list[str]
    port_forwards: list["PortForwardHandle"]


@dataclass
class PortForwardHandle:
    command: str
    process: subprocess.Popen

    def stop(self) -> None:
        if self.process.poll() is None:
            try:
                self.process.terminate()
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()


def load_deploy_config(manifest_path: Path) -> DeployConfig:
    manifest_path = manifest_path.resolve()
    if not manifest_path.exists():
        raise DeployConfigError(
            f"Deploy manifest not found at {manifest_path}. Add one or pass --manifest."
        )

    try:
        data = yaml.safe_load(manifest_path.read_text()) or {}
    except yaml.YAMLError as exc:
        raise DeployConfigError(f"Unable to parse YAML manifest: {exc}") from exc

    service = data.get("service") or {}
    service_name = service.get("name")
    service_mode = (service.get("type") or service.get("mode") or "").lower()
    if not service_name:
        raise DeployConfigError("service.name is required in the deploy manifest.")
    if service_mode not in {"stateless", "stateful"}:
        raise DeployConfigError("service.type must be 'stateless' or 'stateful'.")

    deploy = data.get("deploy") or {}
    target = (deploy.get("target") or "kubernetes").lower()
    namespace = deploy.get("namespace")
    if target not in {"kubernetes", "k8s"}:
        raise DeployConfigError(f"Unsupported deploy target '{target}'. Only kubernetes is supported right now.")
    target = "kubernetes"  # normalize shorthand

    workloads_data = data.get("workloads")
    if workloads_data is None:
        manifest_entries = deploy.get("manifests") or deploy.get("manifest")
        if manifest_entries is None:
            raise DeployConfigError(
                "Define 'workloads' or 'deploy.manifests' in the deploy manifest."
            )
        if isinstance(manifest_entries, (str, Path)):
            manifest_entries = [manifest_entries]
        workloads_data = [
            {
                "name": service_name,
                "mode": service_mode,
                "manifests": [entry],
                "image": data.get("image"),
            }
            for entry in manifest_entries
        ]

    workloads: List[Workload] = []
    for index, raw in enumerate(workloads_data):
        workload_name = raw.get("name") or f"{service_name}-{index + 1}"
        workload_mode = (raw.get("type") or raw.get("mode") or service_mode).lower()
        manifest_value = raw.get("manifests") or raw.get("manifest") or raw.get("path")
        if not manifest_value:
            raise DeployConfigError(f"Workload '{workload_name}' is missing a manifest path.")

        manifest_values = manifest_value if isinstance(manifest_value, list) else [manifest_value]
        resolved_manifests: List[Path] = []
        for manifest_entry in manifest_values:
            manifest_path_value = Path(manifest_entry)
            resolved_manifest = (manifest_path.parent / manifest_path_value).resolve()
            if not resolved_manifest.exists():
                raise DeployConfigError(
                    f"Manifest path not found for workload '{workload_name}': {manifest_path_value}"
                )
            resolved_manifests.append(resolved_manifest)

        image_data = raw.get("image") or data.get("image") or {}
        image = None
        repository = image_data.get("repository") or image_data.get("name")
        if repository:
            image = ImageRef(
                repository=repository,
                tag=image_data.get("tag", "latest"),
                registry=image_data.get("registry"),
            )

        build_data = raw.get("build") or {}
        context_value = build_data.get("context")
        dockerfile_value = build_data.get("dockerfile")
        build_context = (manifest_path.parent / context_value).resolve() if context_value else None
        dockerfile = (manifest_path.parent / dockerfile_value).resolve() if dockerfile_value else None

        port_forward_data = raw.get("portForward") or raw.get("port_forward")
        port_forward = None
        if port_forward_data:
            service_name = port_forward_data.get("service") or workload_name
            remote_port = int(port_forward_data.get("port") or port_forward_data.get("targetPort") or 0)
            local_port = port_forward_data.get("localPort") or port_forward_data.get("local_port")
            if remote_port <= 0:
                raise DeployConfigError(f"Workload '{workload_name}' portForward.port/targetPort must be set.")
            port_forward = PortForward(
                service=service_name,
                remote_port=remote_port,
                local_port=int(local_port) if local_port else None,
            )

        workloads.append(
            Workload(
                name=workload_name,
                mode=workload_mode,
                manifests=resolved_manifests,
                image=image,
                build_context=build_context,
                dockerfile=dockerfile,
                port_forward=port_forward,
            )
        )

    return DeployConfig(
        service=service_name,
        mode=service_mode,
        target=target,
        namespace=namespace,
        workloads=workloads,
    )


def filter_workloads(config: DeployConfig, names: Sequence[str]) -> DeployConfig:
    if not names:
        return config

    selected = [w for w in config.workloads if w.name in names]
    missing = [n for n in names if n not in {w.name for w in config.workloads}]
    if missing:
        raise DeployConfigError(f"Workloads not found: {', '.join(missing)}")
    return DeployConfig(
        service=config.service,
        mode=config.mode,
        target=config.target,
        namespace=config.namespace,
        workloads=selected,
    )


@dataclass
class ValidationResult:
    errors: list[str]
    warnings: list[str]

    @property
    def ok(self) -> bool:
        return not self.errors


def validate_deploy_config(config: DeployConfig) -> ValidationResult:
    errors: list[str] = []
    warnings: list[str] = []

    if not config.workloads:
        errors.append("No workloads defined in manifest.")

    seen_names: set[str] = set()
    for workload in config.workloads:
        if workload.name in seen_names:
            errors.append(f"Duplicate workload name '{workload.name}'.")
        seen_names.add(workload.name)

        for manifest in workload.manifests:
            if not manifest.exists():
                errors.append(f"Manifest missing for workload '{workload.name}': {manifest}")

        if workload.image is None:
            warnings.append(f"Workload '{workload.name}' is missing an image definition.")

        if workload.build_context and not workload.build_context.exists():
            errors.append(f"Build context not found for workload '{workload.name}': {workload.build_context}")

        if workload.dockerfile and not workload.dockerfile.exists():
            errors.append(f"Dockerfile not found for workload '{workload.name}': {workload.dockerfile}")

        if workload.port_forward and workload.port_forward.remote_port <= 0:
            errors.append(f"Port-forward target port invalid for workload '{workload.name}'.")

    return ValidationResult(errors=errors, warnings=warnings)


def format_plan(config: DeployConfig) -> str:
    lines = [
        f"Service: {config.service} ({config.mode})",
        f"Target: {config.target}",
        f"Namespace: {config.namespace or '(default)'}",
        "",
        "Workloads:",
    ]

    for workload in config.workloads:
        image_str = workload.image.as_string() if workload.image else "unspecified"
        manifest_str = ", ".join(str(m) for m in workload.manifests)
        pf = workload.port_forward
        pf_str = f", port-forward svc/{pf.service}:{pf.local_port or pf.remote_port}->{pf.remote_port}" if pf else ""
        lines.append(
            f"- {workload.name} [{workload.mode}]"
            f" -> manifests {manifest_str}"
            f" (image: {image_str}{pf_str})"
        )

    return "\n".join(lines)


def _kubectl_args(kubeconfig: Optional[Path] = None, context: Optional[str] = None) -> list[str]:
    args: list[str] = []
    if kubeconfig:
        args.extend(["--kubeconfig", str(kubeconfig)])
    if context:
        args.extend(["--context", context])
    return args


def _default_port_forward_runner(cmd: list[str]) -> subprocess.Popen:
    return subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)


def _subprocess_runner(verbose: bool):
    def _run(cmd: list[str]):
        return subprocess.run(cmd, check=False, text=True, capture_output=not verbose)

    return _run


def deploy(
    config: DeployConfig,
    *,
    dry_run: bool = False,
    verbose: bool = False,
    port_forward: bool = False,
    kubeconfig: Optional[Path] = None,
    context: Optional[str] = None,
    run_cmd=None,
    port_forward_runner=None,
    progress: ProgressReporter | None = None,
    emit=None,
) -> DeployOutcome:
    runner = run_cmd or _subprocess_runner(verbose)
    port_forward_runner = port_forward_runner or _default_port_forward_runner
    printer = emit or print
    progress = progress or ProgressReporter()

    executed: list[str] = []
    forwards: list[PortForwardHandle] = []
    if config.target != "kubernetes":
        raise DeployConfigError(f"Unsupported target '{config.target}'.")

    kube_args = _kubectl_args(kubeconfig, context)

    for workload in config.workloads:
        for manifest in workload.manifests:
            cmd = ["kubectl", *kube_args, "apply", "-f", str(manifest)]
            if config.namespace:
                cmd.extend(["-n", config.namespace])

            rendered = " ".join(cmd)
            executed.append(rendered)

            with progress.step(f"Applying {manifest.name}") as step:
                if dry_run:
                    step.mark("skipped")
                    continue

                result = runner(cmd)
                return_code = getattr(result, "returncode", 1)
                if return_code != 0:
                    stderr = getattr(result, "stderr", "") or getattr(result, "stdout", "")
                    raise DeployConfigError(
                        f"kubectl apply failed for workload '{workload.name}' (exit {return_code}): {stderr}"
                    )

                if verbose:
                    stdout = getattr(result, "stdout", "") or ""
                    stderr = getattr(result, "stderr", "") or ""
                    if stdout.strip():
                        printer(stdout.strip())
                    if stderr.strip():
                        printer(stderr.strip())

        if port_forward and workload.port_forward:
            pf = workload.port_forward
            local = pf.local_port or pf.remote_port
            cmd = [
                "kubectl",
                *kube_args,
                "port-forward",
                f"svc/{pf.service}",
                f"{local}:{pf.remote_port}",
            ]
            if config.namespace:
                cmd.extend(["-n", config.namespace])

            rendered = " ".join(cmd)
            executed.append(rendered)

            with progress.step(f"Port-forward {pf.service} {local}->{pf.remote_port}") as step:
                if dry_run:
                    step.mark("skipped")
                    continue
                process = port_forward_runner(cmd)
                forwards.append(PortForwardHandle(command=rendered, process=process))
                step.mark("running")

    return DeployOutcome(commands=executed, port_forwards=forwards)


def destroy(
    config: DeployConfig,
    *,
    dry_run: bool = False,
    verbose: bool = False,
    kubeconfig: Optional[Path] = None,
    context: Optional[str] = None,
    run_cmd=None,
    progress: ProgressReporter | None = None,
    emit=None,
) -> Iterable[str]:
    runner = run_cmd or _subprocess_runner(verbose)
    printer = emit or print
    progress = progress or ProgressReporter()

    executed: list[str] = []
    if config.target != "kubernetes":
        raise DeployConfigError(f"Unsupported target '{config.target}'.")

    kube_args = _kubectl_args(kubeconfig, context)

    for workload in config.workloads:
        for manifest in workload.manifests:
            cmd = ["kubectl", *kube_args, "delete", "-f", str(manifest)]
            if config.namespace:
                cmd.extend(["-n", config.namespace])

            rendered = " ".join(cmd)
            executed.append(rendered)

            with progress.step(f"Deleting {manifest.name}") as step:
                if dry_run:
                    step.mark("skipped")
                    continue

                result = runner(cmd)
                return_code = getattr(result, "returncode", 1)
                if return_code != 0:
                    stderr = getattr(result, "stderr", "") or getattr(result, "stdout", "")
                    raise DeployConfigError(
                        f"kubectl delete failed for workload '{workload.name}' (exit {return_code}): {stderr}"
                    )

                if verbose:
                    stdout = getattr(result, "stdout", "") or ""
                    stderr = getattr(result, "stderr", "") or ""
                    if stdout.strip():
                        printer(stdout.strip())
                    if stderr.strip():
                        printer(stderr.strip())

    return executed


def build_images(
    config: DeployConfig,
    *,
    dry_run: bool = False,
    verbose: bool = False,
    run_cmd=None,
    progress: ProgressReporter | None = None,
    emit=None,
) -> Iterable[str]:
    runner = run_cmd or _subprocess_runner(verbose)
    printer = emit or print
    progress = progress or ProgressReporter()

    executed: list[str] = []
    for workload in config.workloads:
        if not workload.image:
            raise DeployConfigError(f"Workload '{workload.name}' is missing an image definition.")

        context = workload.build_context or manifest_default_context(config, workload)
        dockerfile = workload.dockerfile or context / "Dockerfile"

        cmd = [
            "docker",
            "build",
            "-t",
            workload.image.as_string(),
            "-f",
            str(dockerfile),
            str(context),
        ]
        rendered = " ".join(cmd)
        executed.append(rendered)

        with progress.step(f"Building {workload.name}") as step:
            if dry_run:
                step.mark("skipped")
                continue

            result = runner(cmd)
            return_code = getattr(result, "returncode", 1)
            if return_code != 0:
                stderr = getattr(result, "stderr", "") or getattr(result, "stdout", "")
                raise DeployConfigError(
                    f"Docker build failed for workload '{workload.name}' (exit {return_code}): {stderr}"
                )

            if verbose:
                stdout = getattr(result, "stdout", "") or ""
                stderr = getattr(result, "stderr", "") or ""
                if stdout.strip():
                    printer(stdout.strip())
                if stderr.strip():
                    printer(stderr.strip())

    return executed


def push_images(
    config: DeployConfig,
    *,
    dry_run: bool = False,
    verbose: bool = False,
    run_cmd=None,
    progress: ProgressReporter | None = None,
    emit=None,
) -> Iterable[str]:
    runner = run_cmd or _subprocess_runner(verbose)
    printer = emit or print
    progress = progress or ProgressReporter()

    executed: list[str] = []
    for workload in config.workloads:
        if not workload.image:
            raise DeployConfigError(f"Workload '{workload.name}' is missing an image definition.")

        image_ref = workload.image.as_string()
        cmd = ["docker", "push", image_ref]
        rendered = " ".join(cmd)
        executed.append(rendered)

        with progress.step(f"Pushing {workload.name}") as step:
            if dry_run:
                step.mark("skipped")
                continue

            result = runner(cmd)
            return_code = getattr(result, "returncode", 1)
            if return_code != 0:
                stderr = getattr(result, "stderr", "") or getattr(result, "stdout", "")
                raise DeployConfigError(
                    f"Docker push failed for workload '{workload.name}' (exit {return_code}): {stderr}"
                )

            if verbose:
                stdout = getattr(result, "stdout", "") or ""
                stderr = getattr(result, "stderr", "") or ""
                if stdout.strip():
                    printer(stdout.strip())
                if stderr.strip():
                    printer(stderr.strip())

    return executed


def publish(
    config: DeployConfig,
    *,
    dry_run: bool = False,
    verbose: bool = False,
    run_cmd=None,
    progress: ProgressReporter | None = None,
    emit=None,
) -> Iterable[str]:
    progress = progress or ProgressReporter()

    build_cmds = build_images(
        config,
        dry_run=dry_run,
        verbose=verbose,
        run_cmd=run_cmd,
        progress=progress,
        emit=emit,
    )
    push_cmds = push_images(
        config,
        dry_run=dry_run,
        verbose=verbose,
        run_cmd=run_cmd,
        progress=progress,
        emit=emit,
    )
    return list(build_cmds) + list(push_cmds)


def manifest_default_context(config: DeployConfig, workload: Workload) -> Path:
    # Default context: manifest directory for the first manifest of the workload.
    return workload.manifests[0].parent


def stop_port_forwards(handles: Iterable[PortForwardHandle]) -> None:
    for handle in handles:
        try:
            handle.stop()
        except Exception:
            # Best-effort cleanup; ignore termination errors.
            pass


def wait_on_port_forwards(handles: Iterable[PortForwardHandle], *, poll_interval: float = 0.5) -> None:
    active = list(handles)
    if not active:
        return

    try:
        while any(h.process.poll() is None for h in active):
            time.sleep(poll_interval)
    except KeyboardInterrupt:
        pass
    finally:
        stop_port_forwards(active)

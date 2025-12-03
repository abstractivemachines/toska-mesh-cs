from __future__ import annotations

import json
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional

from .progress import ProgressReporter


class KubectlError(Exception):
    """Raised when kubectl commands fail."""


@dataclass
class DeploymentInfo:
    name: str
    namespace: str
    ready: str
    available: int
    desired: int
    images: List[str] = field(default_factory=list)
    labels: Dict[str, str] = field(default_factory=dict)


@dataclass
class ServiceInfo:
    name: str
    namespace: str
    svc_type: str
    cluster_ip: str
    ports: str
    selector: Dict[str, str] = field(default_factory=dict)


@dataclass
class PodInfo:
    name: str
    namespace: str
    ready: str
    status: str
    restarts: int
    node: str


def _kubectl_args(kubeconfig: Optional[Path] = None, context: Optional[str] = None) -> List[str]:
    args: List[str] = []
    if kubeconfig:
        args.extend(["--kubeconfig", str(kubeconfig)])
    if context:
        args.extend(["--context", context])
    return args


def list_deployments(
    *,
    namespace: str,
    selector: Optional[str] = None,
    kubeconfig: Optional[Path] = None,
    context: Optional[str] = None,
    run_cmd=None,
) -> List[DeploymentInfo]:
    runner = run_cmd or (lambda cmd: subprocess.run(cmd, capture_output=True, text=True))
    cmd = ["kubectl", *_kubectl_args(kubeconfig, context), "get", "deploy", "-n", namespace, "-o", "json"]
    if selector:
        cmd.extend(["-l", selector])

    result = runner(cmd)
    if getattr(result, "returncode", 1) != 0:
        raise KubectlError(getattr(result, "stderr", "") or getattr(result, "stdout", ""))

    try:
        payload = json.loads(result.stdout or "{}")
    except json.JSONDecodeError as exc:
        raise KubectlError(f"Unable to parse kubectl output: {exc}") from exc

    deployments: List[DeploymentInfo] = []
    for item in payload.get("items", []):
        meta = item.get("metadata", {})
        status = item.get("status", {})
        spec = item.get("spec", {}) or {}
        template = spec.get("template", {}) or {}
        containers = (template.get("spec") or {}).get("containers") or []
        images = [c.get("image", "") for c in containers if c.get("image")]
        desired = status.get("replicas", 0) or 0
        available = status.get("availableReplicas", 0) or 0
        ready = f"{status.get('readyReplicas', 0) or 0}/{desired}"
        deployments.append(
            DeploymentInfo(
                name=meta.get("name", ""),
                namespace=meta.get("namespace", namespace),
                ready=ready,
                available=available,
                desired=desired,
                images=images,
                labels=meta.get("labels", {}) or {},
            )
        )
    return deployments


def list_services(
    *,
    namespace: str,
    selector: Optional[str] = None,
    kubeconfig: Optional[Path] = None,
    context: Optional[str] = None,
    run_cmd=None,
) -> List[ServiceInfo]:
    runner = run_cmd or (lambda cmd: subprocess.run(cmd, capture_output=True, text=True))
    cmd = ["kubectl", *_kubectl_args(kubeconfig, context), "get", "svc", "-n", namespace, "-o", "json"]
    if selector:
        cmd.extend(["-l", selector])

    result = runner(cmd)
    if getattr(result, "returncode", 1) != 0:
        raise KubectlError(getattr(result, "stderr", "") or getattr(result, "stdout", ""))

    try:
        payload = json.loads(result.stdout or "{}")
    except json.JSONDecodeError as exc:
        raise KubectlError(f"Unable to parse kubectl output: {exc}") from exc

    services: List[ServiceInfo] = []
    for item in payload.get("items", []):
        meta = item.get("metadata", {})
        spec = item.get("spec", {}) or {}
        ports_data = spec.get("ports") or []
        ports = ", ".join(
            f"{p.get('port')}->{p.get('targetPort') or ''}".strip("->")
            for p in ports_data
            if p.get("port")
        )
        services.append(
            ServiceInfo(
                name=meta.get("name", ""),
                namespace=meta.get("namespace", namespace),
                svc_type=spec.get("type", "ClusterIP"),
                cluster_ip=spec.get("clusterIP", ""),
                ports=ports,
                selector=spec.get("selector", {}) or {},
            )
        )
    return services


def list_pods(
    *,
    namespace: str,
    selector: Optional[str] = None,
    kubeconfig: Optional[Path] = None,
    context: Optional[str] = None,
    run_cmd=None,
) -> List[PodInfo]:
    runner = run_cmd or (lambda cmd: subprocess.run(cmd, capture_output=True, text=True))
    cmd = ["kubectl", *_kubectl_args(kubeconfig, context), "get", "pods", "-n", namespace, "-o", "json"]
    if selector:
        cmd.extend(["-l", selector])

    result = runner(cmd)
    if getattr(result, "returncode", 1) != 0:
        raise KubectlError(getattr(result, "stderr", "") or getattr(result, "stdout", ""))

    try:
        payload = json.loads(result.stdout or "{}")
    except json.JSONDecodeError as exc:
        raise KubectlError(f"Unable to parse kubectl output: {exc}") from exc

    pods: List[PodInfo] = []
    for item in payload.get("items", []):
        meta = item.get("metadata", {})
        status = item.get("status", {}) or {}
        container_statuses = status.get("containerStatuses") or []
        ready_count = sum(1 for cs in container_statuses if cs.get("ready"))
        total = len(container_statuses)
        ready = f"{ready_count}/{total or 0}"
        restarts = sum(int(cs.get("restartCount", 0) or 0) for cs in container_statuses)
        pods.append(
            PodInfo(
                name=meta.get("name", ""),
                namespace=meta.get("namespace", namespace),
                ready=ready,
                status=status.get("phase", ""),
                restarts=restarts,
                node=status.get("nodeName", ""),
            )
        )
    return pods


def format_deployments_table(deployments: Iterable[DeploymentInfo]) -> str:
    rows = [["NAME", "READY", "AVAILABLE", "IMAGES"]]
    for d in deployments:
        images = ", ".join(d.images) if d.images else "-"
        rows.append([d.name, d.ready, str(d.available), images])
    return _format_table(rows)


def format_services_table(services: Iterable[ServiceInfo]) -> str:
    rows = [["NAME", "TYPE", "CLUSTER IP", "PORTS"]]
    for s in services:
        ports = s.ports or "-"
        rows.append([s.name, s.svc_type, s.cluster_ip or "-", ports])
    return _format_table(rows)


def format_pods_table(pods: Iterable[PodInfo]) -> str:
    rows = [["NAME", "READY", "STATUS", "RESTARTS", "NODE"]]
    for p in pods:
        rows.append([p.name, p.ready, p.status or "-", str(p.restarts), p.node or "-"])
    return _format_table(rows)


def _format_table(rows: List[List[str]]) -> str:
    if not rows:
        return ""
    widths = [max(len(row[i]) for row in rows) for i in range(len(rows[0]))]
    lines = []
    for idx, row in enumerate(rows):
        padded = "  ".join(col.ljust(widths[i]) for i, col in enumerate(row))
        lines.append(padded)
        if idx == 0:
            lines.append("  ".join("-" * w for w in widths))
    return "\n".join(lines)


def gather_service_info(
    *,
    namespace: str,
    selector: Optional[str],
    include_deployments: bool = True,
    include_services: bool = True,
    include_pods: bool = False,
    kubeconfig: Optional[Path] = None,
    context: Optional[str] = None,
    run_cmd=None,
    progress: ProgressReporter | None = None,
) -> dict:
    progress = progress or ProgressReporter()

    deployments: List[DeploymentInfo] = []
    if include_deployments:
        with progress.step("Listing deployments"):
            deployments = list_deployments(
                namespace=namespace,
                selector=selector,
                kubeconfig=kubeconfig,
                context=context,
                run_cmd=run_cmd,
            )

    services: List[ServiceInfo] = []
    if include_services:
        with progress.step("Listing services"):
            services = list_services(
                namespace=namespace,
                selector=selector,
                kubeconfig=kubeconfig,
                context=context,
                run_cmd=run_cmd,
            )

    pods: List[PodInfo] = []
    if include_pods:
        with progress.step("Listing pods"):
            pods = list_pods(
                namespace=namespace,
                selector=selector,
                kubeconfig=kubeconfig,
                context=context,
                run_cmd=run_cmd,
            )

    return {
        "deployments": deployments,
        "services": services,
        "pods": pods,
    }

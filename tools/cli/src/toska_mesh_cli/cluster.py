from __future__ import annotations

import ipaddress
import socket
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from pathlib import Path
from shutil import which
from typing import Iterable, Sequence

import yaml


class KubeconfigError(Exception):
    """Raised when kubeconfig generation fails."""


@dataclass
class TalosContext:
    endpoints: list[str]
    nodes: list[str]


@dataclass
class KubeconfigResult:
    path: str
    endpoints: list[str]
    nodes: list[str]


def _load_talos_context(talosconfig: Path) -> TalosContext:
    try:
        data = yaml.safe_load(talosconfig.read_text()) or {}
    except yaml.YAMLError as exc:
        raise KubeconfigError(f"Unable to parse talosconfig at {talosconfig}: {exc}") from exc

    context_name = data.get("context")
    contexts = data.get("contexts") or {}
    context_data = contexts.get(context_name) or {}

    endpoints = [str(ep) for ep in context_data.get("endpoints") or data.get("endpoints") or [] if ep]
    nodes = [str(node) for node in context_data.get("nodes") or data.get("nodes") or [] if node]
    return TalosContext(endpoints=endpoints, nodes=nodes)


def _resolve_talosconfig_path(talosconfig: Path, *, base_dir: Path | None = None) -> Path:
    base_dir = base_dir or Path.cwd()
    candidate = talosconfig.expanduser()
    if candidate.is_absolute():
        return candidate

    search_roots = [base_dir, *base_dir.parents]
    for root in search_roots:
        probe = (root / candidate).resolve()
        if probe.exists():
            return probe

    return (base_dir / candidate).resolve()


def _iter_hosts(cidrs: Iterable[str], *, max_hosts: int) -> list[str]:
    hosts: list[str] = []
    seen: set[str] = set()
    for cidr in cidrs:
        try:
            network = ipaddress.ip_network(cidr, strict=False)
        except ValueError as exc:
            raise KubeconfigError(f"Invalid CIDR '{cidr}': {exc}") from exc
        for host in network.hosts():
            host_str = str(host)
            if host_str in seen:
                continue
            seen.add(host_str)
            hosts.append(host_str)
            if len(hosts) >= max_hosts:
                return hosts
    return hosts


def discover_talos_endpoints(
    cidrs: Sequence[str],
    *,
    port: int = 50000,
    max_hosts: int = 256,
    timeout: float = 0.2,
    max_workers: int = 64,
) -> list[str]:
    if port <= 0 or port > 65535:
        raise KubeconfigError(f"Invalid port {port}; must be between 1 and 65535.")
    if max_hosts <= 0:
        raise KubeconfigError("max_hosts must be greater than zero.")
    if not cidrs:
        return []

    hosts = _iter_hosts(cidrs, max_hosts=max_hosts)
    if not hosts:
        return []

    found: list[str] = []

    def _probe(host: str) -> str | None:
        try:
            with socket.create_connection((host, port), timeout=timeout):
                return host
        except OSError:
            return None

    workers = min(max_workers, len(hosts)) or 1
    with ThreadPoolExecutor(max_workers=workers) as executor:
        futures = {executor.submit(_probe, host): host for host in hosts}
        for future in as_completed(futures):
            result = future.result()
            if result:
                found.append(result)

    return found


def talos_kubeconfig(
    *,
    talosconfig: Path,
    endpoints: Sequence[str] | None,
    nodes: Sequence[str] | None,
    out: Path,
    force: bool = False,
    verbose: bool = False,
    discover_cidrs: Sequence[str] | None = None,
    discover_port: int = 50000,
    discover_timeout: float = 0.2,
    max_hosts: int = 256,
    max_workers: int = 64,
    run_cmd=None,
) -> KubeconfigResult:
    if which("talosctl") is None and run_cmd is None:
        raise KubeconfigError("talosctl is required on PATH to generate a kubeconfig.")

    talosconfig = _resolve_talosconfig_path(talosconfig)
    if not talosconfig.exists():
        raise KubeconfigError(f"talosconfig not found at {talosconfig}")

    derived = _load_talos_context(talosconfig)
    endpoints_list: list[str] = list(endpoints or []) or derived.endpoints
    nodes_list: list[str] = list(nodes or []) or derived.nodes

    if not endpoints_list and discover_cidrs:
        discovered = discover_talos_endpoints(
            discover_cidrs,
            port=discover_port,
            max_hosts=max_hosts,
            timeout=discover_timeout,
            max_workers=max_workers,
        )
        endpoints_list = discovered
        if nodes_list == []:
            nodes_list = discovered

    if not endpoints_list:
        raise KubeconfigError("No endpoints supplied and none found in talosconfig.")
    if nodes_list == []:
        nodes_list = endpoints_list

    out_path = out.expanduser()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    cmd: list[str] = [
        "talosctl",
        "--talosconfig",
        str(talosconfig),
        "--endpoints",
        ",".join(endpoints_list),
    ]
    if nodes_list:
        cmd.extend(["--nodes", ",".join(nodes_list)])
    cmd.extend(["kubeconfig", str(out_path)])
    if force:
        cmd.append("--force")

    runner = run_cmd or (lambda c: subprocess.run(c, check=False, text=True, capture_output=not verbose))
    result = runner(cmd)
    return_code = getattr(result, "returncode", 1)
    if return_code != 0:
        stderr = getattr(result, "stderr", "") or getattr(result, "stdout", "")
        raise KubeconfigError(f"talosctl kubeconfig failed (exit {return_code}): {stderr}")

    try:
        out_path.chmod(0o600)
    except Exception:
        # Best effort; leave existing permissions if chmod fails.
        pass

    return KubeconfigResult(path=str(out_path), endpoints=endpoints_list, nodes=nodes_list)

from __future__ import annotations

import argparse
import sys
from dataclasses import replace
from pathlib import Path
from typing import Sequence
from shutil import which

from . import __version__
from .progress import ProgressReporter


def _require_commands(commands: Sequence[str], action: str) -> None:
    missing = [cmd for cmd in commands if which(cmd) is None]
    if missing:
        formatted = ", ".join(missing)
        raise RuntimeError(f"{action} requires command(s) on PATH: {formatted}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="toska",
        description="Command-line interface for Toska Mesh (placeholder scaffold).",
    )
    parser.add_argument(
        "--version",
        action="version",
        version=f"%(prog)s {__version__}",
    )

    subparsers = parser.add_subparsers(
        dest="command",
        title="commands",
        metavar="COMMAND",
        help="Action to perform (to be expanded).",
    )

    subparsers.add_parser(
        "info",
        help="Show placeholder CLI information.",
        description="Temporary command until concrete workflows are implemented.",
    )

    kubeconfig_parser = subparsers.add_parser(
        "kubeconfig",
        help="Generate a kubeconfig using talosctl.",
        description="Generate a kubeconfig file for a Talos-backed cluster.",
    )
    kubeconfig_parser.add_argument(
        "--talosconfig",
        type=Path,
        default=Path("clusterconfig") / "talosconfig",
        help="Path to talosconfig (default: clusterconfig/talosconfig).",
    )
    kubeconfig_parser.add_argument(
        "-e",
        "--endpoint",
        dest="endpoints",
        action="append",
        help="Talos endpoint(s); if omitted, use endpoints from talosconfig.",
    )
    kubeconfig_parser.add_argument(
        "--node",
        dest="nodes",
        action="append",
        help="Talos node(s); if omitted, use nodes from talosconfig when present.",
    )
    kubeconfig_parser.add_argument(
        "-o",
        "--out",
        type=Path,
        default=Path.home() / ".kube" / "config",
        help="Output kubeconfig path (default: ~/.kube/config).",
    )
    kubeconfig_parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite existing kubeconfig at the output path.",
    )
    kubeconfig_parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show talosctl output while generating kubeconfig.",
    )
    kubeconfig_parser.add_argument(
        "--discover-cidr",
        action="append",
        dest="discover_cidr",
        help="CIDR(s) to scan for Talos endpoints (opt-in).",
    )
    kubeconfig_parser.add_argument(
        "--discover-port",
        type=int,
        default=50000,
        help="Port to probe during discovery (default: 50000).",
    )
    kubeconfig_parser.add_argument(
        "--discover-timeout",
        type=float,
        default=0.2,
        help="TCP timeout per host during discovery in seconds (default: 0.2).",
    )
    kubeconfig_parser.add_argument(
        "--max-hosts",
        type=int,
        default=256,
        help="Maximum hosts to probe across all CIDRs (default: 256).",
    )

    validate_parser = subparsers.add_parser(
        "validate",
        help="Validate a Toska Mesh manifest.",
        description="Validate a Toska Mesh deploy manifest and report errors/warnings.",
    )
    validate_parser.add_argument(
        "-f",
        "--manifest",
        default="toska.yaml",
        help="Path to a deploy manifest (default: toska.yaml).",
    )
    validate_parser.add_argument(
        "--json",
        action="store_true",
        help="Emit validation output as JSON.",
    )

    deploy_parser = subparsers.add_parser(
        "deploy",
        help="Deploy a user service to a target environment.",
        description=(
            "Deploy a Toska Mesh user service (stateless or stateful) using a manifest in the current directory."
        ),
    )
    deploy_parser.add_argument(
        "-f",
        "--manifest",
        default="toska.yaml",
        help="Path to a deploy manifest (default: toska.yaml).",
    )
    deploy_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the deployment plan without applying it.",
    )
    deploy_parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show kubectl output while applying manifests.",
    )
    deploy_parser.add_argument(
        "--port-forward",
        action="store_true",
        help="Start kubectl port-forward for workloads that define portForward in the manifest.",
    )
    deploy_parser.add_argument(
        "-w",
        "--workload",
        action="append",
        help="Limit deploy to specific workload(s) by name.",
    )
    deploy_parser.add_argument(
        "--kubeconfig",
        type=Path,
        help="Path to kubeconfig file (passed to kubectl).",
    )
    deploy_parser.add_argument(
        "--context",
        help="Kube context to target (passed to kubectl).",
    )
    deploy_parser.add_argument(
        "-n",
        "--namespace",
        help="Override namespace defined in the manifest.",
    )

    destroy_parser = subparsers.add_parser(
        "destroy",
        help="Delete a user service from the target environment.",
        description="Delete Toska Mesh user service resources defined in the manifest.",
    )
    destroy_parser.add_argument(
        "-f",
        "--manifest",
        default="toska.yaml",
        help="Path to a deploy manifest (default: toska.yaml).",
    )
    destroy_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the deletion plan without applying it.",
    )
    destroy_parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show kubectl output while deleting manifests.",
    )
    destroy_parser.add_argument(
        "-w",
        "--workload",
        action="append",
        help="Limit deletion to specific workload(s) by name.",
    )
    destroy_parser.add_argument(
        "--kubeconfig",
        type=Path,
        help="Path to kubeconfig file (passed to kubectl).",
    )
    destroy_parser.add_argument(
        "--context",
        help="Kube context to target (passed to kubectl).",
    )
    destroy_parser.add_argument(
        "-n",
        "--namespace",
        help="Override namespace defined in the manifest.",
    )

    build_parser = subparsers.add_parser(
        "build",
        help="Build container images for workloads defined in the manifest.",
    )
    build_parser.add_argument(
        "-f",
        "--manifest",
        default="toska.yaml",
        help="Path to a deploy manifest (default: toska.yaml).",
    )
    build_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print build commands without executing them.",
    )
    build_parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show docker output while building images.",
    )
    build_parser.add_argument(
        "-w",
        "--workload",
        action="append",
        help="Limit builds to specific workload(s) by name.",
    )

    push_parser = subparsers.add_parser(
        "push",
        help="Push container images for workloads defined in the manifest.",
    )
    push_parser.add_argument(
        "-f",
        "--manifest",
        default="toska.yaml",
        help="Path to a deploy manifest (default: toska.yaml).",
    )
    push_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print push commands without executing them.",
    )
    push_parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show docker output while pushing images.",
    )
    push_parser.add_argument(
        "-w",
        "--workload",
        action="append",
        help="Limit pushes to specific workload(s) by name.",
    )

    publish_parser = subparsers.add_parser(
        "publish",
        help="Build and push container images defined in the manifest.",
    )
    publish_parser.add_argument(
        "-f",
        "--manifest",
        default="toska.yaml",
        help="Path to a deploy manifest (default: toska.yaml).",
    )
    publish_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print build/push commands without executing them.",
    )
    publish_parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show docker output while building/pushing images.",
    )
    publish_parser.add_argument(
        "-w",
        "--workload",
        action="append",
        help="Limit publish to specific workload(s) by name.",
    )

    services_parser = subparsers.add_parser(
        "services",
        help="List deployed Toska Mesh user services.",
    )
    services_parser.add_argument(
        "--namespace",
        default="toskamesh",
        help="Kubernetes namespace to query (default: toskamesh).",
    )
    services_parser.add_argument(
        "-l",
        "--selector",
        default="component=example",
        help="Label selector to filter user services (default: component=example). Use --all to disable.",
    )
    services_parser.add_argument(
        "--all",
        action="store_true",
        help="List all services without a selector (may include core components).",
    )
    services_parser.add_argument(
        "--json",
        action="store_true",
        help="Emit raw JSON for scripting.",
    )
    services_parser.add_argument(
        "--kubeconfig",
        type=Path,
        help="Path to kubeconfig file (passed to kubectl).",
    )
    services_parser.add_argument(
        "--context",
        help="Kube context to target (passed to kubectl).",
    )

    deployments_parser = subparsers.add_parser(
        "deployments",
        help="List Toska Mesh user deployments.",
    )
    deployments_parser.add_argument(
        "--namespace",
        default="toskamesh",
        help="Kubernetes namespace to query (default: toskamesh).",
    )
    deployments_parser.add_argument(
        "-l",
        "--selector",
        default="component=example",
        help="Label selector to filter user deployments (default: component=example). Use --all to disable.",
    )
    deployments_parser.add_argument(
        "--all",
        action="store_true",
        help="List all deployments without a selector (may include core components).",
    )
    deployments_parser.add_argument(
        "--json",
        action="store_true",
        help="Emit raw JSON for scripting.",
    )
    deployments_parser.add_argument(
        "--kubeconfig",
        type=Path,
        help="Path to kubeconfig file (passed to kubectl).",
    )
    deployments_parser.add_argument(
        "--context",
        help="Kube context to target (passed to kubectl).",
    )

    status_parser = subparsers.add_parser(
        "status",
        help="Show deployments, services, and pods for Toska Mesh workloads.",
    )
    status_parser.add_argument(
        "--namespace",
        default="toskamesh",
        help="Kubernetes namespace to query (default: toskamesh).",
    )
    status_parser.add_argument(
        "-l",
        "--selector",
        default="component=example",
        help="Label selector to filter workloads (default: component=example). Use --all to disable.",
    )
    status_parser.add_argument(
        "--all",
        action="store_true",
        help="List all resources without a selector (may include core components).",
    )
    status_parser.add_argument(
        "--json",
        action="store_true",
        help="Emit raw JSON for scripting.",
    )
    status_parser.add_argument(
        "--kubeconfig",
        type=Path,
        help="Path to kubeconfig file (passed to kubectl).",
    )
    status_parser.add_argument(
        "--context",
        help="Kube context to target (passed to kubectl).",
    )

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    reporter = ProgressReporter()
    rich_output = reporter.console is not None

    if args.command == "info":
        with reporter.step("Info"):
            pass
        print(f"Toska Mesh CLI v{__version__} placeholder: define commands next.")
        reporter.summarize()
        return 0

    if args.command == "kubeconfig":
        from .cluster import KubeconfigError, talos_kubeconfig

        try:
            _require_commands(["talosctl"], "Kubeconfig")
            with reporter.step("Generating kubeconfig"):
                result = talos_kubeconfig(
                    talosconfig=Path(args.talosconfig),
                    endpoints=args.endpoints,
                    nodes=args.nodes,
                    out=Path(args.out),
                    force=args.force,
                    verbose=args.verbose,
                    discover_cidrs=args.discover_cidr,
                    discover_port=args.discover_port,
                    discover_timeout=args.discover_timeout,
                    max_hosts=args.max_hosts,
                )
            print(f"Wrote kubeconfig to {result.path}")
            if result.endpoints:
                print(f"Endpoints: {', '.join(result.endpoints)}")
            reporter.summarize()
            return 0
        except (KubeconfigError, RuntimeError) as exc:
            print(f"Kubeconfig failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    if args.command == "validate":
        import json

        from .deploy import DeployConfigError, load_deploy_config, validate_deploy_config

        manifest_path = Path(args.manifest)

        try:
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)
            result = validate_deploy_config(config)

            if args.json:
                payload = {
                    "errors": result.errors,
                    "warnings": result.warnings,
                    "ok": result.ok,
                }
                print(json.dumps(payload, indent=2))
                return 0 if result.ok else 1

            if result.errors:
                print("Validation errors:")
                for err in result.errors:
                    print(f"- {err}")
            if result.warnings:
                print("\nWarnings:")
                for warn in result.warnings:
                    print(f"- {warn}")

            if result.ok:
                print("Manifest is valid.")
                reporter.summarize()
                return 0
            reporter.summarize()
            return 1
        except DeployConfigError as exc:
            print(f"Validate failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    if args.command == "deploy":
        from .deploy import (
            DeployConfigError,
            deploy,
            filter_workloads,
            format_plan,
            load_deploy_config,
            stop_port_forwards,
            wait_on_port_forwards,
        )

        manifest_path = Path(args.manifest)
        forward_handles = []

        try:
            if not args.dry_run:
                _require_commands(["kubectl"], "Deploy")
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)
                if args.workload:
                    config = filter_workloads(config, args.workload)
                if args.namespace:
                    config = replace(config, namespace=args.namespace)

            with reporter.step("Building deployment plan"):
                plan = format_plan(config)

            show_plan = args.verbose or args.dry_run
            if show_plan:
                print(plan)

            result = deploy(
                config,
                dry_run=args.dry_run,
                verbose=args.verbose,
                port_forward=args.port_forward,
                kubeconfig=args.kubeconfig,
                context=args.context,
                progress=reporter,
            )
            forward_handles = result.port_forwards

            if show_plan:
                header = "Planned commands:" if args.dry_run else "Executed commands:"
                print(f"\n{header}")
                for command in result.commands:
                    print(f"- {command}")

            if args.port_forward and not args.dry_run:
                if result.port_forwards:
                    print("\nPort-forwarding; press Ctrl+C to stop.")
                    wait_on_port_forwards(result.port_forwards)
                else:
                    print("\nNo workloads with portForward defined; nothing to port-forward.")
            reporter.summarize()
            return 0
        except (DeployConfigError, RuntimeError) as exc:
            print(f"Deploy failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1
        finally:
            if forward_handles:
                stop_port_forwards(forward_handles)

    if args.command == "destroy":
        from .deploy import DeployConfigError, destroy, filter_workloads, format_plan, load_deploy_config

        manifest_path = Path(args.manifest)

        try:
            if not args.dry_run:
                _require_commands(["kubectl"], "Destroy")
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)
                if args.workload:
                    config = filter_workloads(config, args.workload)
                if args.namespace:
                    config = replace(config, namespace=args.namespace)

            with reporter.step("Building deletion plan"):
                plan = format_plan(config)

            show_plan = args.verbose or args.dry_run
            if show_plan:
                print(plan)

            commands = destroy(
                config,
                dry_run=args.dry_run,
                verbose=args.verbose,
                kubeconfig=args.kubeconfig,
                context=args.context,
                progress=reporter,
            )

            if show_plan:
                header = "Planned commands:" if args.dry_run else "Executed commands:"
                print(f"\n{header}")
                for command in commands:
                    print(f"- {command}")
            reporter.summarize()
            return 0
        except (DeployConfigError, RuntimeError) as exc:
            print(f"Destroy failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    if args.command in {"build", "push", "publish"}:
        from .deploy import (
            DeployConfigError,
            build_images,
            filter_workloads,
            format_plan,
            load_deploy_config,
            publish,
            push_images,
        )

        manifest_path = Path(args.manifest)

        try:
            if not args.dry_run:
                _require_commands(["docker"], args.command.capitalize())
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)
                if args.workload:
                    config = filter_workloads(config, args.workload)

            with reporter.step("Building image plan"):
                plan = format_plan(config)

            show_plan = args.verbose or args.dry_run
            commands: list[str] = []

            if args.command == "build":
                if show_plan:
                    print(plan)
                commands = build_images(
                    config,
                    dry_run=args.dry_run,
                    verbose=args.verbose,
                    progress=reporter,
                )
            elif args.command == "push":
                if show_plan:
                    print(plan)
                commands = push_images(
                    config,
                    dry_run=args.dry_run,
                    verbose=args.verbose,
                    progress=reporter,
                )
            else:  # publish
                if show_plan:
                    print(plan)
                commands = publish(
                    config,
                    dry_run=args.dry_run,
                    verbose=args.verbose,
                    progress=reporter,
                )

            if show_plan:
                header = "Planned commands:" if args.dry_run else "Executed commands:"
                print(f"\n{header}")
                for command in commands:
                    print(f"- {command}")
            reporter.summarize()
            return 0
        except (DeployConfigError, RuntimeError) as exc:
            print(f"{args.command.capitalize()} failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    if args.command == "services":
        import json

        from .info import (
            KubectlError,
            format_deployments_table,
            format_services_table,
            gather_service_info,
        )

        selector = None if args.all else args.selector
        try:
            _require_commands(["kubectl"], "Services")
            data = gather_service_info(
                namespace=args.namespace,
                selector=selector,
                include_deployments=False,
                include_services=True,
                kubeconfig=args.kubeconfig,
                context=args.context,
                progress=reporter,
            )

            if args.json:
                serializable = {
                    "deployments": [d.__dict__ for d in data["deployments"]],
                    "services": [s.__dict__ for s in data["services"]],
                }
                print(json.dumps(serializable, indent=2))
                return 0

            if data["services"]:
                print("\nServices")
                print(format_services_table(data["services"], rich_output=rich_output))
            else:
                print("\nServices: none found")
            reporter.summarize()
            return 0
        except (KubectlError, RuntimeError) as exc:
            print(f"Services failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    if args.command == "deployments":
        import json

        from .info import KubectlError, format_deployments_table, gather_service_info

        selector = None if args.all else args.selector
        try:
            _require_commands(["kubectl"], "Deployments")
            data = gather_service_info(
                namespace=args.namespace,
                selector=selector,
                include_deployments=True,
                include_services=False,
                kubeconfig=args.kubeconfig,
                context=args.context,
                progress=reporter,
            )

            if args.json:
                serializable = {
                    "deployments": [d.__dict__ for d in data["deployments"]],
                }
                print(json.dumps(serializable, indent=2))
                return 0

            if data["deployments"]:
                print("\nDeployments")
                print(format_deployments_table(data["deployments"], rich_output=rich_output))
            else:
                print("\nDeployments: none found")
            reporter.summarize()
            return 0
        except (KubectlError, RuntimeError) as exc:
            print(f"Deployments failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    if args.command == "status":
        import json

        from .info import (
            KubectlError,
            format_deployments_table,
            format_pods_table,
            format_services_table,
            gather_service_info,
        )

        selector = None if args.all else args.selector
        try:
            _require_commands(["kubectl"], "Status")
            data = gather_service_info(
                namespace=args.namespace,
                selector=selector,
                include_deployments=True,
                include_services=True,
                include_pods=True,
                kubeconfig=args.kubeconfig,
                context=args.context,
                progress=reporter,
            )

            if args.json:
                serializable = {
                    "deployments": [d.__dict__ for d in data["deployments"]],
                    "services": [s.__dict__ for s in data["services"]],
                    "pods": [p.__dict__ for p in data["pods"]],
                }
                print(json.dumps(serializable, indent=2))
                return 0

            if data["deployments"]:
                print("\nDeployments")
                print(format_deployments_table(data["deployments"], rich_output=rich_output))
            else:
                print("\nDeployments: none found")

            if data["services"]:
                print("\nServices")
                print(format_services_table(data["services"], rich_output=rich_output))
            else:
                print("\nServices: none found")

            if data["pods"]:
                print("\nPods")
                print(format_pods_table(data["pods"], rich_output=rich_output))
            else:
                print("\nPods: none found")
            reporter.summarize()
            return 0
        except (KubectlError, RuntimeError) as exc:
            print(f"Status failed: {exc}", file=sys.stderr)
            reporter.summarize()
            return 1

    # Default to help when no command is provided.
    parser.print_help()
    return 0

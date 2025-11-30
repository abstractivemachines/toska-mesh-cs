from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Sequence

from . import __version__
from .progress import ProgressReporter


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

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    reporter = ProgressReporter()

    if args.command == "info":
        with reporter.step("Info"):
            pass
        print(f"Toska Mesh CLI v{__version__} placeholder: define commands next.")
        return 0

    if args.command == "deploy":
        from .deploy import DeployConfigError, deploy, format_plan, load_deploy_config

        manifest_path = Path(args.manifest)

        try:
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)

            with reporter.step("Building deployment plan"):
                plan = format_plan(config)

            show_plan = args.verbose or args.dry_run
            if show_plan:
                print(plan)

            commands = deploy(
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
            return 0
        except DeployConfigError as exc:
            print(f"Deploy failed: {exc}", file=sys.stderr)
            return 1

    if args.command == "destroy":
        from .deploy import DeployConfigError, destroy, format_plan, load_deploy_config

        manifest_path = Path(args.manifest)

        try:
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)

            with reporter.step("Building deletion plan"):
                plan = format_plan(config)

            show_plan = args.verbose or args.dry_run
            if show_plan:
                print(plan)

            commands = destroy(
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
            return 0
        except DeployConfigError as exc:
            print(f"Destroy failed: {exc}", file=sys.stderr)
            return 1

    if args.command in {"build", "push", "publish"}:
        from .deploy import (
            DeployConfigError,
            build_images,
            format_plan,
            load_deploy_config,
            publish,
            push_images,
        )

        manifest_path = Path(args.manifest)

        try:
            with reporter.step(f"Loading manifest {manifest_path}"):
                config = load_deploy_config(manifest_path)

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
            return 0
        except DeployConfigError as exc:
            print(f"{args.command.capitalize()} failed: {exc}", file=sys.stderr)
            return 1

    # Default to help when no command is provided.
    parser.print_help()
    return 0

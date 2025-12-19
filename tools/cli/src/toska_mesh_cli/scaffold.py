from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass
from importlib import resources
from pathlib import Path
from shutil import which
from typing import Iterable, Sequence


class ScaffoldError(Exception):
    """Raised when scaffolding a service fails."""


@dataclass(frozen=True)
class ScaffoldResult:
    output_dir: Path
    created_files: list[Path]
    added_projects: list[Path]


@dataclass(frozen=True)
class ScaffoldContext:
    service_name: str
    base_name: str
    service_pascal: str
    base_pascal: str
    project_name: str
    service_class_name: str
    api_service_name: str
    silo_service_name: str
    api_project_name: str
    silo_project_name: str
    contracts_project_name: str
    runtime_version: str
    orleans_version: str


def scaffold_service(
    name: str,
    *,
    service_type: str,
    style: str,
    stateful_template: str,
    output_dir: Path,
    runtime_version: str,
    orleans_version: str,
    solution: Path | None,
    force: bool,
) -> ScaffoldResult:
    service_name = _validate_service_name(name)
    template_name = _resolve_template(service_type, style, stateful_template)

    base_name = _base_name(service_name)
    base_pascal = _to_pascal(base_name)
    service_pascal = _to_pascal(service_name)
    project_name = f"{base_pascal}Service"
    service_class_name = project_name
    api_service_name = f"{base_name}-api"
    silo_service_name = f"{base_name}-silo"
    api_project_name = f"{base_pascal}Api"
    silo_project_name = f"{base_pascal}Silo"

    contracts_project_name = (
        f"{service_pascal}.Contracts"
        if template_name == "stateful-local"
        else f"{base_pascal}Contracts"
    )

    context = ScaffoldContext(
        service_name=service_name,
        base_name=base_name,
        service_pascal=service_pascal,
        base_pascal=base_pascal,
        project_name=project_name,
        service_class_name=service_class_name,
        api_service_name=api_service_name,
        silo_service_name=silo_service_name,
        api_project_name=api_project_name,
        silo_project_name=silo_project_name,
        contracts_project_name=contracts_project_name,
        runtime_version=runtime_version,
        orleans_version=orleans_version,
    )

    _ensure_output_dir(output_dir, force=force)
    created_files = _render_template(template_name, output_dir, context)

    added_projects: list[Path] = []
    if solution is not None:
        added_projects = _add_projects_to_solution(solution, output_dir)

    return ScaffoldResult(
        output_dir=output_dir,
        created_files=created_files,
        added_projects=added_projects,
    )


def _resolve_template(service_type: str, style: str, stateful_template: str) -> str:
    service_type = service_type.lower()
    style = style.lower()
    stateful_template = stateful_template.lower()

    if service_type == "stateless":
        if style not in {"host", "base"}:
            raise ScaffoldError("Stateless style must be 'host' or 'base'.")
        return "stateless-host" if style == "host" else "stateless-base"

    if service_type == "stateful":
        if stateful_template not in {"consul", "local"}:
            raise ScaffoldError("Stateful template must be 'consul' or 'local'.")
        return "stateful-consul" if stateful_template == "consul" else "stateful-local"

    raise ScaffoldError("Service type must be 'stateless' or 'stateful'.")


def _ensure_output_dir(output_dir: Path, *, force: bool) -> None:
    if output_dir.exists():
        if not force and any(output_dir.iterdir()):
            raise ScaffoldError(
                f"Output directory '{output_dir}' exists and is not empty. Use --force to overwrite."
            )
    else:
        output_dir.mkdir(parents=True, exist_ok=True)


def _render_template(template_name: str, output_dir: Path, context: ScaffoldContext) -> list[Path]:
    template_resource = resources.files("toska_mesh_cli").joinpath("templates", template_name)
    if not template_resource.is_dir():
        raise ScaffoldError(f"Template '{template_name}' was not found in the CLI package.")

    replacements = _build_replacements(template_name, context)
    ordered_replacements = _ordered_replacements(replacements)
    created: list[Path] = []

    with resources.as_file(template_resource) as template_dir:
        for source in template_dir.rglob("*"):
            if source.is_dir():
                continue
            rel = source.relative_to(template_dir)
            rel_str = _apply_replacements(str(rel), ordered_replacements)
            dest = output_dir / rel_str
            dest.parent.mkdir(parents=True, exist_ok=True)

            data = source.read_bytes()
            try:
                text = data.decode("utf-8")
            except UnicodeDecodeError:
                dest.write_bytes(data)
            else:
                text = _apply_replacements(text, ordered_replacements)
                dest.write_text(text)
            created.append(dest)

    return created


def _build_replacements(template_name: str, context: ScaffoldContext) -> dict[str, str]:
    replacements: dict[str, str] = {
        "0.1.0-preview": context.runtime_version,
        "8.2.0": context.orleans_version,
    }

    if template_name == "stateless-host":
        replacements.update(
            {
                "hello-mesh-service": context.service_name,
                "HelloMeshService": context.project_name,
            }
        )
    elif template_name == "stateless-base":
        replacements.update(
            {
                "adder-mesh-service": context.service_name,
                "AdderMeshService": context.project_name,
                "AdderService": context.service_class_name,
            }
        )
    elif template_name == "stateful-consul":
        replacements.update(
            {
                "todo-mesh-service": context.service_name,
                "todo-mesh-api": context.api_service_name,
                "todo-mesh-silo": context.silo_service_name,
                "TodoMeshService": context.service_pascal,
                "TodoMeshApi": context.api_project_name,
                "TodoMeshSilo": context.silo_project_name,
                "TodoMeshContracts": context.contracts_project_name,
            }
        )
    elif template_name == "stateful-local":
        replacements.update(
            {
                "redis-grain-demo": context.service_name,
                "redis-grain-api": context.api_service_name,
                "redis-grain-silo": context.silo_service_name,
                "RedisGrainDemo.Contracts": context.contracts_project_name,
                "RedisGrainDemo": context.service_pascal,
                "RedisGrainApi": context.api_project_name,
                "RedisGrainSilo": context.silo_project_name,
            }
        )
    else:
        raise ScaffoldError(f"Unsupported template '{template_name}'.")

    return replacements


def _add_projects_to_solution(solution: Path, output_dir: Path) -> list[Path]:
    solution_path = solution.expanduser().resolve()
    if not solution_path.exists():
        raise ScaffoldError(f"Solution file not found at '{solution_path}'.")

    if which("dotnet") is None:
        raise ScaffoldError("Adding projects to a solution requires 'dotnet' on PATH.")

    projects = sorted(output_dir.rglob("*.csproj"))
    if not projects:
        raise ScaffoldError("No .csproj files found to add to the solution.")

    command = ["dotnet", "sln", str(solution_path), "add", *[str(p) for p in projects]]
    subprocess.run(command, check=True)
    return projects


def _validate_service_name(name: str) -> str:
    candidate = name.strip()
    if not candidate:
        raise ScaffoldError("Service name is required.")

    if not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?", candidate):
        raise ScaffoldError(
            "Service name must be lowercase letters/numbers with dashes (e.g., my-mesh-service)."
        )

    return candidate


def _base_name(service_name: str) -> str:
    if service_name.endswith("-service"):
        trimmed = service_name[: -len("-service")]
        return trimmed or service_name
    return service_name


def _to_pascal(value: str) -> str:
    parts = re.split(r"[-_\s]+", value)
    return "".join(part.capitalize() for part in parts if part)


def _ordered_replacements(replacements: dict[str, str]) -> Sequence[tuple[str, str]]:
    return tuple(sorted(replacements.items(), key=lambda item: len(item[0]), reverse=True))


def _apply_replacements(text: str, replacements: Iterable[tuple[str, str]]) -> str:
    for src, dest in replacements:
        if src:
            text = text.replace(src, dest)
    return text

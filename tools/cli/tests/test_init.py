from pathlib import Path

from toska_mesh_cli.cli import main


def test_init_stateless_host_scaffold(tmp_path):
    output_dir = tmp_path / "inventory-service"

    exit_code = main(
        [
            "init",
            "inventory-service",
            "--type",
            "stateless",
            "--style",
            "host",
            "--output",
            str(output_dir),
        ]
    )

    assert exit_code == 0
    assert (output_dir / "InventoryService.csproj").exists()
    program_text = (output_dir / "Program.cs").read_text()
    assert "inventory-service" in program_text


def test_init_adds_projects_to_solution(monkeypatch, tmp_path):
    solution = tmp_path / "Demo.sln"
    solution.write_text("")
    output_dir = tmp_path / "metrics-service"

    called: dict[str, list[str]] = {}

    monkeypatch.setattr("toska_mesh_cli.scaffold.which", lambda name: "/usr/bin/dotnet")

    def fake_run(cmd, check):
        called["cmd"] = cmd
        return None

    monkeypatch.setattr("toska_mesh_cli.scaffold.subprocess.run", fake_run)

    exit_code = main(
        [
            "init",
            "metrics-service",
            "--type",
            "stateless",
            "--style",
            "base",
            "--output",
            str(output_dir),
            "--solution",
            str(solution),
        ]
    )

    assert exit_code == 0
    assert called["cmd"][:3] == ["dotnet", "sln", str(solution)]
    assert "add" in called["cmd"]
    assert any(Path(arg).suffix == ".csproj" for arg in called["cmd"])

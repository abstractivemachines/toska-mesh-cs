from toska_mesh_cli import __version__
from toska_mesh_cli.cli import build_parser, main


def test_build_parser_sets_prog_and_description():
    parser = build_parser()

    assert parser.prog == "toska"
    assert parser.description


def test_info_command_emits_placeholder(capsys):
    exit_code = main(["info"])
    captured = capsys.readouterr()
    output = f"{captured.out}{captured.err}".lower()

    assert exit_code == 0
    assert "placeholder" in output
    assert __version__ in captured.out or __version__ in captured.err


def test_validate_command_succeeds(tmp_path, capsys):
    manifest = tmp_path / "toska.yaml"
    k8s_dir = tmp_path / "k8s"
    k8s_dir.mkdir()
    (k8s_dir / "service.yaml").write_text("apiVersion: v1\nkind: Service\n")
    manifest.write_text(
        """
service:
  name: sample-service
  type: stateless
deploy:
  manifests:
    - k8s/service.yaml
workloads:
  - name: sample-service
    type: stateless
    manifests:
      - k8s/service.yaml
"""
    )

    exit_code = main(["validate", "-f", str(manifest)])
    captured = capsys.readouterr()

    assert exit_code == 0
    assert "valid" in captured.out.lower()


def test_cli_kubeconfig_delegates_to_helper(monkeypatch, tmp_path):
    from pathlib import Path

    called: dict = {}

    monkeypatch.setattr("toska_mesh_cli.cli._require_commands", lambda commands, action: None)

    def fake_kubeconfig(**kwargs):
        from toska_mesh_cli.cluster import KubeconfigResult

        called.update(kwargs)
        return KubeconfigResult(path="generated", endpoints=["1.2.3.4"], nodes=["1.2.3.5"])

    monkeypatch.setattr("toska_mesh_cli.cluster.talos_kubeconfig", lambda **kwargs: fake_kubeconfig(**kwargs))

    exit_code = main(
        [
            "kubeconfig",
            "--talosconfig",
            str(tmp_path / "talosconfig"),
            "--endpoint",
            "1.2.3.4",
            "--node",
            "1.2.3.5",
            "--out",
            str(tmp_path / "kubeconfig"),
            "--force",
            "--discover-cidr",
            "10.0.0.0/24",
            "--discover-port",
            "50000",
            "--discover-timeout",
            "0.5",
            "--max-hosts",
            "32",
        ]
    )

    assert exit_code == 0
    assert called["endpoints"] == ["1.2.3.4"]
    assert called["nodes"] == ["1.2.3.5"]
    assert called["force"] is True
    assert isinstance(called["talosconfig"], Path)
    assert isinstance(called["out"], Path)
    assert called["discover_cidrs"] == ["10.0.0.0/24"]
    assert called["discover_port"] == 50000
    assert called["discover_timeout"] == 0.5
    assert called["max_hosts"] == 32

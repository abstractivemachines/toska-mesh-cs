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

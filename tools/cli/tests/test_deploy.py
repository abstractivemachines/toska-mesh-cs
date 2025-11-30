import pytest

from toska_mesh_cli.deploy import (
    DeployConfigError,
    build_images,
    deploy,
    destroy,
    load_deploy_config,
    publish,
    push_images,
)


def _write_manifest(tmp_path):
    dockerfile = tmp_path / "Dockerfile"
    dockerfile.write_text("# test")

    silo_dir = tmp_path / "k8s"
    silo_dir.mkdir()
    workload_manifest = silo_dir / "service.yaml"
    workload_manifest.write_text("apiVersion: v1\nkind: Service\n")

    manifest_dir = tmp_path / "k8s"
    manifest_dir.mkdir(exist_ok=True)

    manifest_file = tmp_path / "toska.yaml"
    manifest_file.write_text(
        """
service:
  name: sample-service
  type: stateless
deploy:
  manifests: []
workloads:
  - name: sample-service
    type: stateless
    manifests:
      - k8s/service.yaml
    image:
      repository: sample
      tag: local
    build:
      context: .
      dockerfile: Dockerfile
"""
    )
    return manifest_file


def test_load_deploy_config_parses_single_manifest(tmp_path):
    manifest = _write_manifest(tmp_path)

    config = load_deploy_config(manifest)

    assert config.service == "sample-service"
    assert config.mode == "stateless"
    assert len(config.workloads) == 1
    assert config.workloads[0].manifests[0].name == "service.yaml"


def test_deploy_dry_run_returns_commands(tmp_path):
    manifest = _write_manifest(tmp_path)
    config = load_deploy_config(manifest)

    commands = list(deploy(config, dry_run=True))

    assert commands
    assert "kubectl apply" in commands[0]
    assert "service.yaml" in commands[0]


def test_deploy_verbose_emits_runner_output(tmp_path, capsys):
    manifest = _write_manifest(tmp_path)
    config = load_deploy_config(manifest)

    class Result:
        returncode = 0
        stdout = "applied"
        stderr = ""

    def fake_runner(cmd):
        return Result()

    _ = list(deploy(config, dry_run=False, verbose=True, run_cmd=fake_runner))
    captured = capsys.readouterr()
    assert "applied" in captured.out


def test_destroy_dry_run_returns_delete_commands(tmp_path):
    manifest = _write_manifest(tmp_path)
    config = load_deploy_config(manifest)

    commands = list(destroy(config, dry_run=True))

    assert commands
    assert "kubectl delete" in commands[0]


def test_build_dry_run_returns_commands(tmp_path):
    manifest = _write_manifest(tmp_path)
    config = load_deploy_config(manifest)

    commands = list(build_images(config, dry_run=True))

    assert commands
    assert "docker build" in commands[0]


def test_publish_runs_build_and_push(tmp_path):
    manifest = _write_manifest(tmp_path)
    config = load_deploy_config(manifest)

    calls = []

    class Result:
        returncode = 0
        stdout = ""
        stderr = ""

    def fake_runner(cmd):
        calls.append(cmd)
        return Result()

    _ = list(publish(config, dry_run=False, verbose=False, run_cmd=fake_runner))

    assert any(cmd[0:2] == ["docker", "build"] for cmd in calls)
    assert any(cmd[0:2] == ["docker", "push"] for cmd in calls)


def test_invalid_manifest_path_raises(tmp_path):
    manifest_file = tmp_path / "toska.yaml"
    manifest_file.write_text(
        """
service:
  name: broken-service
  type: stateful
deploy:
  manifests:
    - k8s/missing.yaml
"""
    )

    with pytest.raises(DeployConfigError):
        load_deploy_config(manifest_file)

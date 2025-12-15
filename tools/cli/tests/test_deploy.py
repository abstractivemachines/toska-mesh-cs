import pytest

from toska_mesh_cli.deploy import (
    DeployConfigError,
    build_images,
    deploy,
    destroy,
    filter_workloads,
    load_deploy_config,
    publish,
    push_images,
    validate_deploy_config,
)


def _write_manifest(tmp_path, *, with_port_forward: bool = False):
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
    if with_port_forward:
        manifest_text = manifest_file.read_text()
        manifest_file.write_text(
            manifest_text
            + """
    portForward:
      service: sample-service
      port: 8080
      localPort: 18080
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

    result = deploy(config, dry_run=True)
    commands = result.commands

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

    _ = deploy(config, dry_run=False, verbose=True, run_cmd=fake_runner)
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


def test_port_forward_dry_run_adds_command(tmp_path):
    manifest = _write_manifest(tmp_path, with_port_forward=True)
    config = load_deploy_config(manifest)

    result = deploy(config, dry_run=True, port_forward=True)

    assert any("port-forward" in cmd for cmd in result.commands)


def test_filter_workloads_limits_selection(tmp_path):
    manifest = _write_manifest(tmp_path)
    config = load_deploy_config(manifest)

    filtered = filter_workloads(config, ["sample-service"])
    assert len(filtered.workloads) == 1

    with pytest.raises(DeployConfigError):
        _ = filter_workloads(config, ["missing"])


def test_validate_deploy_config_reports_missing_image(tmp_path):
    manifest = _write_manifest(tmp_path)
    manifest.write_text(manifest.read_text().replace("    image:\n      repository: sample\n      tag: local\n", ""))
    config = load_deploy_config(manifest)

    result = validate_deploy_config(config)
    assert result.warnings
    assert result.ok


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


def test_invalid_workload_mode_rejected(tmp_path):
    manifest_file = tmp_path / "toska.yaml"
    manifest_file.write_text(
        """
service:
  name: broken-service
  type: stateless
workloads:
  - name: broken-workload
    type: invalid
    manifests:
      - k8s/service.yaml
"""
    )

    with pytest.raises(DeployConfigError):
        load_deploy_config(manifest_file)


def test_port_forward_failure_is_reported(tmp_path):
    manifest = _write_manifest(tmp_path, with_port_forward=True)
    config = load_deploy_config(manifest)

    class Result:
        returncode = 0
        stdout = ""
        stderr = ""

    class _Stream:
        def __init__(self, text: str):
            self._text = text

        def read(self):
            return self._text

    class FailingPortForward:
        def __init__(self):
            self.returncode = 1
            self.stdout = _Stream("")
            self.stderr = _Stream("no pods to forward")

        def poll(self):
            return self.returncode

    with pytest.raises(DeployConfigError):
        deploy(
            config,
            dry_run=False,
            port_forward=True,
            run_cmd=lambda cmd: Result(),
            port_forward_runner=lambda cmd: FailingPortForward(),
        )

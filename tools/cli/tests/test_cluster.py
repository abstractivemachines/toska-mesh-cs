import pytest

from toska_mesh_cli.cluster import KubeconfigError, talos_kubeconfig


def test_talos_kubeconfig_uses_talosconfig_defaults_and_writes_output(tmp_path):
    talosconfig = tmp_path / "talosconfig"
    talosconfig.write_text(
        """
context: homek8s
contexts:
  homek8s:
    endpoints:
      - 10.0.0.1
    nodes:
      - 10.0.0.2
"""
    )

    out_path = tmp_path / "kubeconfig"
    commands = []

    class Result:
        returncode = 0
        stdout = ""
        stderr = ""

    def fake_runner(cmd):
        commands.append(cmd)
        out_path.write_text("kubeconfig")
        return Result()

    result = talos_kubeconfig(
        talosconfig=talosconfig,
        endpoints=None,
        nodes=None,
        out=out_path,
        force=True,
        verbose=False,
        run_cmd=fake_runner,
    )

    assert result.path == str(out_path)
    assert result.endpoints == ["10.0.0.1"]
    assert result.nodes == ["10.0.0.2"]
    assert commands
    assert commands[0][0] == "talosctl"
    assert "--endpoints" in commands[0]
    assert "10.0.0.1" in commands[0]
    assert "--nodes" in commands[0]
    assert "10.0.0.2" in commands[0]
    assert "kubeconfig" in commands[0]
    assert str(out_path) in commands[0]
    assert out_path.exists()


def test_talos_kubeconfig_errors_without_endpoints(tmp_path):
    talosconfig = tmp_path / "talosconfig"
    talosconfig.write_text("context: empty\ncontexts:\n  empty: {}\n")

    with pytest.raises(KubeconfigError):
        talos_kubeconfig(
            talosconfig=talosconfig,
            endpoints=None,
            nodes=None,
            out=tmp_path / "kubeconfig",
            force=False,
            verbose=False,
            run_cmd=lambda cmd: None,
        )


def test_discover_talos_endpoints_probes_hosts(monkeypatch):
    from toska_mesh_cli.cluster import discover_talos_endpoints

    calls = []

    def fake_conn(addr, timeout=0.0):
        host, port = addr
        calls.append((host, port, timeout))
        if host.endswith(".2"):
            class _Conn:
                def __enter__(self):
                    return self

                def __exit__(self, exc_type, exc, tb):
                    return False

            return _Conn()
        raise OSError("refused")

    monkeypatch.setattr("toska_mesh_cli.cluster.socket.create_connection", fake_conn)

    result = discover_talos_endpoints(["10.0.0.0/30"], port=1234, timeout=0.5, max_hosts=4, max_workers=2)

    assert sorted(result) == ["10.0.0.2"]
    assert any(call[0] == "10.0.0.1" for call in calls)
    assert any(call[0] == "10.0.0.2" for call in calls)


def test_discover_talos_endpoints_rejects_invalid_cidr():
    from toska_mesh_cli.cluster import discover_talos_endpoints

    with pytest.raises(KubeconfigError):
        discover_talos_endpoints(["not-a-cidr"])


def test_resolve_talosconfig_path_searches_parents(tmp_path):
    from pathlib import Path
    from toska_mesh_cli.cluster import _resolve_talosconfig_path

    repo_root = tmp_path
    talos_dir = repo_root / "clusterconfig"
    talos_dir.mkdir()
    talos_file = talos_dir / "talosconfig"
    talos_file.write_text("context: test\ncontexts:\n  test:\n    endpoints: []\n")

    nested_dir = repo_root / "tools" / "cli"
    nested_dir.mkdir(parents=True)

    resolved = _resolve_talosconfig_path(Path("clusterconfig") / "talosconfig", base_dir=nested_dir)
    assert resolved == talos_file.resolve()

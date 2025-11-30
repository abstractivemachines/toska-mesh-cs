import json

from toska_mesh_cli.info import (
    DeploymentInfo,
    KubectlError,
    ServiceInfo,
    format_deployments_table,
    format_services_table,
    gather_service_info,
    list_deployments,
    list_services,
)


def _fake_result(payload):
    class Result:
        returncode = 0
        stdout = json.dumps(payload)
        stderr = ""

    return Result()


def test_list_deployments_parses_images_and_ready():
    payload = {
        "items": [
            {
                "metadata": {"name": "hello", "namespace": "toskamesh"},
                "status": {"replicas": 2, "readyReplicas": 1, "availableReplicas": 1},
                "spec": {
                    "template": {
                        "spec": {"containers": [{"image": "hello:v1"}, {"image": "sidecar:v2"}]}
                    }
                },
            }
        ]
    }

    deployments = list_deployments(namespace="toskamesh", run_cmd=lambda cmd: _fake_result(payload))

    assert len(deployments) == 1
    assert deployments[0].ready == "1/2"
    assert "hello:v1" in deployments[0].images


def test_list_services_parses_ports():
    payload = {
        "items": [
            {
                "metadata": {"name": "hello-svc", "namespace": "toskamesh"},
                "spec": {
                    "type": "ClusterIP",
                    "clusterIP": "10.0.0.1",
                    "ports": [{"port": 80, "targetPort": 8080}],
                },
            }
        ]
    }

    services = list_services(namespace="toskamesh", run_cmd=lambda cmd: _fake_result(payload))

    assert len(services) == 1
    assert services[0].ports.startswith("80")


def test_format_tables_show_headers():
    deployments = [
        DeploymentInfo(name="svc", namespace="ns", ready="1/1", available=1, desired=1, images=["img"])
    ]
    services = [
        ServiceInfo(
            name="svc",
            namespace="ns",
            svc_type="ClusterIP",
            cluster_ip="10.0.0.1",
            ports="80",
            selector={"app": "svc"},
        )
    ]

    dep_text = format_deployments_table(deployments)
    svc_text = format_services_table(services)
    assert "NAME" in dep_text
    assert "NAME" in svc_text


def test_gather_service_info_passes_selector_and_namespace():
    seen_cmds = []

    def fake_runner(cmd):
        seen_cmds.append(cmd)
        if "deploy" in cmd:
            return _fake_result({"items": []})
        return _fake_result({"items": []})

    data = gather_service_info(
        namespace="toskamesh",
        selector="component=example",
        include_deployments=True,
        include_services=True,
        run_cmd=fake_runner,
    )

    assert data["deployments"] == []
    assert any("-l" in cmd for cmd in seen_cmds)


def test_gather_service_info_skips_deployments_when_disabled():
    seen_cmds = []

    def fake_runner(cmd):
        seen_cmds.append(cmd)
        return _fake_result({"items": []})

    data = gather_service_info(
        namespace="toskamesh",
        selector=None,
        include_deployments=False,
        include_services=True,
        run_cmd=fake_runner,
    )

    assert data["deployments"] == []
    assert any("svc" in cmd for cmd in seen_cmds)
    assert not any("deploy" in cmd for cmd in seen_cmds)


def test_kubectl_error_bubbles():
    class Result:
        returncode = 1
        stdout = ""
        stderr = "boom"

    try:
        list_deployments(namespace="ns", run_cmd=lambda cmd: Result())
    except KubectlError as exc:
        assert "boom" in str(exc)
    else:
        assert False, "Expected KubectlError"

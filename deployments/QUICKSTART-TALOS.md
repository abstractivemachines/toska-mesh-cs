# ToskaMesh Talos Quick Start (Local Network)

Deploy ToskaMesh to the single-node Talos cluster (no AWS/EKS). Assumes your Talos control plane is reachable at `192.168.50.229` and your laptop is on the same network (registry will run on the laptop at `192.168.50.73:5000`; adjust if your IP changes).

## Prerequisites

- `talosctl`, `kubectl`, `helm`, and `docker` installed on the laptop.
- Talos kubeconfig set (e.g., `kubectl config use-context admin@homek8s-1`).
- Talos node reachable: `ping 192.168.50.229` and `talosctl --talosconfig clusterconfig/talosconfig version -n 192.168.50.229`.

## 1) Add a default storage class
Talos has no default `StorageClass`. Install a simple hostPath provisioner:

```bash
helm repo add rancher-local-path https://rancher.github.io/local-path-provisioner
helm repo update
helm install local-path-provisioner rancher-local-path/local-path-provisioner \
  --namespace local-path-storage --create-namespace \
  --set storageClass.defaultClass=true

kubectl get sc
```

## 2) Run a local registry on the laptop

```bash
docker run -d --restart=always -p 5000:5000 --name registry registry:2
REGISTRY=192.168.50.73:5000   # replace if your laptop IP differs
```

## 3) Allow the Talos node to pull from the registry

Patch the machine config to trust the local (HTTP) registry, then reboot to apply:

```bash
talosctl edit machineconfig --talosconfig clusterconfig/talosconfig -n 192.168.50.229
```

Add under `machine`:
```yaml
machine:
  registries:
    mirrors:
      "192.168.50.73:5000":
        endpoints:
          - http://192.168.50.73:5000
```
Save and exit, then:
```bash
talosctl apply-config --talosconfig clusterconfig/talosconfig -n 192.168.50.229 --mode=autoreboot --file /var/run/talos/machineconfig
```
Wait for the node to return (`talosctl version -n 192.168.50.229`).

## 4) Build and push images to the local registry
Use the provided Dockerfiles; tag with `local` and push to the registry:

```bash
REGISTRY=192.168.50.73:5000
TAG=local
for svc in gateway discovery auth-service config-service metrics-service tracing-service core health-monitor; do
  file="deployments/Dockerfile.${svc^}"
  # handle dash to PascalCase file names
  file=$(echo $file | sed 's/Auth-service/AuthService/;s/Config-service/ConfigService/;s/Metrics-service/MetricsService/;s/Tracing-service/TracingService/;s/Health-monitor/HealthMonitor/')
  docker build -t ${REGISTRY}/toskamesh-${svc}:${TAG} -f ${file} .
  docker push ${REGISTRY}/toskamesh-${svc}:${TAG}
done
```

## 5) Install infra dependencies in-cluster

```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo add hashicorp https://helm.releases.hashicorp.com
helm repo update

kubectl create namespace toskamesh-infra

helm install postgres bitnami/postgresql -n toskamesh-infra \
  --set auth.database=toksa_mesh \
  --set auth.username=toksa_mesh \
  --set auth.password=toksa_mesh_password \
  --set primary.persistence.size=5Gi \
  --set global.storageClass=local-path

helm install rabbitmq bitnami/rabbitmq -n toskamesh-infra \
  --set auth.username=guest \
  --set auth.password=guest \
  --set persistence.storageClass=local-path

helm install redis bitnami/redis -n toskamesh-infra \
  --set auth.enabled=false \
  --set master.persistence.storageClass=local-path

helm install consul hashicorp/consul -n toskamesh-infra \
  --set global.name=consul \
  --set server.replicas=1 \
  --set ui.enabled=true
```

## 6) Deploy ToskaMesh to Talos

Use the Talos-focused values file (`helm/toskamesh/values-talos.yaml`) and override secrets as needed:

```bash
kubectl create namespace toskamesh

helm install toskamesh ./helm/toskamesh \
  --namespace toskamesh \
  -f helm/toskamesh/values-talos.yaml \
  --set global.meshServiceAuthSecret=REPLACE_ME_32_CHARS \
  --set gateway.secrets.jwtSecretKey=REPLACE_ME_32_CHARS
```

## 7) Verify and access

```bash
kubectl get pods -n toskamesh -w
kubectl get svc -n toskamesh toskamesh-gateway
```

Gateway is exposed via NodePort `30080` on the Talos node:

```bash
curl http://192.168.50.229:30080/health
```

## 8) Iterating

- Rebuild/re-push images with a new tag and upgrade:
  ```bash
  TAG=local2
  # rebuild/push as in step 4
  helm upgrade toskamesh ./helm/toskamesh -n toskamesh \
    -f helm/toskamesh/values-talos.yaml \
    --set gateway.image.tag=$TAG \
    --set discovery.image.tag=$TAG \
    --set authService.image.tag=$TAG \
    --set configService.image.tag=$TAG \
    --set metricsService.image.tag=$TAG \
    --set tracingService.image.tag=$TAG \
    --set core.image.tag=$TAG \
    --set healthMonitor.image.tag=$TAG
  ```
- Logs: `kubectl logs -n toskamesh -l app.kubernetes.io/instance=toskamesh --tail=100 -f`
- Cleanup: `helm uninstall toskamesh -n toskamesh && kubectl delete ns toskamesh`

## 9) Optional: Install monitoring (Grafana via NodePort)

Expose Grafana directly on the Talos node (no ingress required):

```bash
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

kubectl create namespace monitoring

helm upgrade --install prometheus prometheus-community/kube-prometheus-stack \
  -n monitoring \
  -f deployments/monitoring-values-talos.yaml
```

- Access: `http://192.168.50.229:30300` (adjust if your Talos node IP changes).
- Password: `kubectl -n monitoring get secret prometheus-grafana -o jsonpath='{.data.admin-password}' | base64 -d; echo`
- Dashboards: import JSON files from `deployments/grafana/dashboards/` into Grafana.
- Note: `deployments/monitoring-values-talos.yaml` disables `nodeExporter` to satisfy Talos PodSecurity (restricted). If you want host metrics, relax the policy and re-enable it.

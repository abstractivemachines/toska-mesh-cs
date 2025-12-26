# ToskaMesh Kubernetes Deployment Guide

This guide provides instructions for deploying ToskaMesh Service Mesh Control Plane to Kubernetes using Helm.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Local Development Deployment](#local-development-deployment)
- [Production Deployment](#production-deployment)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [Upgrading](#upgrading)
- [Uninstalling](#uninstalling)

## Prerequisites

### Required Tools

- **kubectl** (v1.27+): Kubernetes command-line tool
- **Helm** (v3.12+): Kubernetes package manager
- **Docker** (v24.0+): For building container images
- **Kubernetes cluster**:
  - Local: Docker Desktop, Minikube, or Kind
  - Cloud: EKS, AKS, GKE, or similar

### External Services

ToskaMesh requires the following external services:

- **PostgreSQL 15+**: Database for Auth, Config, Metrics, Tracing services, and Orleans persistence
- **Consul 1.16+**: Service registry and discovery
- **RabbitMQ 3.12+**: Message broker for event-driven communication
- **Redis 7+**: (Optional) Distributed caching

## Quick Start

### 1. Build Docker Images

From the repository root:

```bash
# Build all service images
docker build -f deployments/Dockerfile.Gateway -t toskamesh-gateway:latest .
docker build -f deployments/Dockerfile.Discovery -t toskamesh-discovery:latest .
docker build -f deployments/Dockerfile.AuthService -t toskamesh-auth-service:latest .
docker build -f deployments/Dockerfile.ConfigService -t toskamesh-config-service:latest .
docker build -f deployments/Dockerfile.MetricsService -t toskamesh-metrics-service:latest .
docker build -f deployments/Dockerfile.TracingService -t toskamesh-tracing-service:latest .
docker build -f deployments/Dockerfile.Core -t toskamesh-core:latest .
docker build -f deployments/Dockerfile.HealthMonitor -t toskamesh-health-monitor:latest .
```

### 2. Deploy Using Helm

```bash
# Install the chart
helm install toskamesh ./helm/toskamesh

# Or with custom values
helm install toskamesh ./helm/toskamesh -f ./helm/toskamesh/values.yaml
```

### 3. Verify Deployment

```bash
# Check pod status
kubectl get pods -l app.kubernetes.io/name=toskamesh

# Check services
kubectl get svc -l app.kubernetes.io/name=toskamesh

# View logs
  kubectl logs -l app.kubernetes.io/component=gateway --tail=100
```

### 4. Enable Ingress and Autoscaling (optional)

```bash
# Enable gateway ingress + HPA via Helm values
helm upgrade toskamesh ./helm/toskamesh \
  --reuse-values \
  --set gateway.ingress.enabled=true \
  --set gateway.ingress.hosts[0].host=toskamesh.local \
  --set gateway.hpa.enabled=true

# Check ingress and HPA resources
kubectl get ingress toskamesh-gateway
kubectl get hpa
```

### 5. Service Mesh Integration (optional)

Set `mesh.enabled=true` to inject a service-mesh sidecar (e.g., Istio/Linkerd) and apply common mesh annotations to all pods.

```bash
helm upgrade toskamesh ./helm/toskamesh \
  --reuse-values \
  --set mesh.enabled=true

# Verify pods include the sidecar
kubectl get pods -l app.kubernetes.io/name=toskamesh -o yaml | grep sidecar.istio.io/inject
```

## Local Development Deployment

### Using Docker Desktop Kubernetes

1. **Enable Kubernetes in Docker Desktop**
   - Open Docker Desktop Settings
   - Navigate to Kubernetes
   - Check "Enable Kubernetes"
   - Apply & Restart

2. **Start Infrastructure Services**

   ```bash
   # Start infrastructure using docker-compose
   cd deployments
   docker-compose up -d postgres consul rabbitmq redis
   ```

3. **Build and Load Images**

   ```bash
   # Build images
   docker build -f deployments/Dockerfile.Gateway -t toskamesh-gateway:latest .
   # ... (repeat for all services)

   # Images are automatically available to Docker Desktop K8s
   ```

4. **Deploy with Helm (Development Values)**

   ```bash
   helm install toskamesh ./helm/toskamesh \
     --set externalServices.postgres.host=host.docker.internal \
     --set externalServices.consul.address=http://host.docker.internal:8500 \
     --set externalServices.rabbitmq.host=host.docker.internal
   ```

5. **Access the Gateway**

   ```bash
   # Get the Gateway LoadBalancer IP (or localhost for Docker Desktop)
   kubectl get svc toskamesh-gateway

   # Test
   curl http://localhost/health
   ```

### Using Minikube

1. **Start Minikube**

   ```bash
   minikube start --cpus=4 --memory=8192
   ```

2. **Use Minikube's Docker Daemon**

   ```bash
   eval $(minikube docker-env)
   ```

3. **Build Images** (using Minikube's Docker daemon)

   ```bash
   docker build -f deployments/Dockerfile.Gateway -t toskamesh-gateway:latest .
   # ... (repeat for all services)
   ```

4. **Deploy Infrastructure to Minikube**

   ```bash
   # Deploy Postgres
   kubectl run postgres --image=postgres:15-alpine \
     --env="POSTGRES_USER=toksa_mesh" \
     --env="POSTGRES_PASSWORD=toksa_mesh_password" \
     --env="POSTGRES_DB=toksa_mesh"
   kubectl expose pod postgres --port=5432

   # Deploy Consul
   kubectl run consul --image=consul:1.16 \
     --command -- agent -server -ui -bootstrap-expect=1 -client=0.0.0.0
   kubectl expose pod consul --port=8500

   # Deploy RabbitMQ
   kubectl run rabbitmq --image=rabbitmq:3.12-management-alpine
   kubectl expose pod rabbitmq --port=5672 --name=rabbitmq
   ```

5. **Install ToskaMesh**

   ```bash
   helm install toskamesh ./helm/toskamesh
   ```

6. **Access Services**

   ```bash
   # Get Gateway URL
   minikube service toskamesh-gateway --url
   ```

## Production Deployment

### Prerequisites

- Managed PostgreSQL database (AWS RDS, Azure Database, Cloud SQL)
- Managed Consul cluster or external Consul service
- Managed RabbitMQ (AWS MQ, Azure Service Bus, CloudAMQP)
- Container registry (ECR, ACR, GCR, Docker Hub)

### 1. Build and Push Images to Registry

```bash
# Set your registry
REGISTRY=your-registry.example.com

# Build and tag
docker build -f deployments/Dockerfile.Gateway -t ${REGISTRY}/toskamesh-gateway:1.0.0 .
docker build -f deployments/Dockerfile.Discovery -t ${REGISTRY}/toskamesh-discovery:1.0.0 .
# ... (repeat for all services)

# Push to registry
docker push ${REGISTRY}/toskamesh-gateway:1.0.0
docker push ${REGISTRY}/toskamesh-discovery:1.0.0
# ... (repeat for all services)
```

### 2. Create Production Values File

Create `values-custom-prod.yaml`:

```yaml
environment: Production

externalServices:
  consul:
    address: "https://consul.production.example.com:8500"
  postgres:
    host: "postgres.rds.amazonaws.com"
    database: "toskamesh_prod"
    username: "toskamesh"
    password: "USE_SECRETS_MANAGER"
  rabbitmq:
    host: "rabbitmq.cloudamqp.com"
    username: "toskamesh"
    password: "USE_SECRETS_MANAGER"

gateway:
  replicaCount: 3
  image:
    repository: your-registry.example.com/toskamesh-gateway
    tag: "1.0.0"
  resources:
    requests:
      cpu: 500m
      memory: 512Mi
    limits:
      cpu: 2000m
      memory: 2Gi

# ... (configure all other services)
```

### 3. Secrets (use managed sources)

- Use cloud secret manager + ExternalSecrets (or pre-created K8s secrets) instead of embedding secrets in values.
- Configure the chart to point at existing secrets:

```bash
cat <<'EOF' > values-custom-prod.yaml
secrets:
  meshServiceAuth:
    existingSecret: mesh-auth-secret
    key: mesh_service_auth_secret
  defaultConnection:
    existingSecret: app-db-conn
    key: db_connection_string
  gatewayJwt:
    existingSecret: gateway-jwt-secret
    key: jwt_secret_key

gateway:
  ingress:
    tls:
      - secretName: toskamesh-gateway-tls   # create via cert-manager or upload cert/key
        hosts: ["gateway.example.com"]
EOF
```

Create the referenced secrets using your preferred mechanism (e.g., ExternalSecrets, `kubectl create secret generic`, or cert-manager for TLS).

### 4. Deploy to Production

```bash
helm install toskamesh ./helm/toskamesh \
  --namespace toskamesh-prod \
  --values ./helm/toskamesh/values-prod.yaml \
  --values ./values-custom-prod.yaml \
  --wait
```

### 5. Verify Production Deployment

```bash
# Check all pods are running
kubectl get pods -n toskamesh-prod

# Check Gateway external IP
kubectl get svc toskamesh-gateway -n toskamesh-prod

# Run smoke tests
curl https://your-gateway-url/health
```

## Configuration

### Environment Variables

The following environment variables can be configured via Helm values:

#### Global Settings

- `environment`: ASP.NET Core environment (Development, Staging, Production)
- `secrets.meshServiceAuth.existingSecret/key`: External secret for inter-service auth (falls back to `global.meshServiceAuthSecret` when unset)
- `secrets.defaultConnection.existingSecret/key`: External secret for DB connection strings
- `secrets.gatewayJwt.existingSecret/key`: External secret for gateway JWT signing key

#### External Services

- `externalServices.consul.address`: Consul server address
- `externalServices.postgres.host`: PostgreSQL host
- `externalServices.rabbitmq.host`: RabbitMQ host

#### Per-Service Settings

Each service supports:
- `enabled`: Enable/disable service deployment
- `replicaCount`: Number of pod replicas
- `image.repository`: Docker image repository
- `image.tag`: Docker image tag
- `image.pullPolicy`: Image pull policy (IfNotPresent, Always, Never)
- `resources`: CPU and memory requests/limits
- `networkPolicy.enabled`: Enable namespace-scoped ingress/egress policies (enabled in prod values)
- `mesh.enabled`: When true, Istio/mesh sidecar annotations are injected on all workloads

### Scaling

```bash
# Scale Gateway to 5 replicas
helm upgrade toskamesh ./helm/toskamesh \
  --set gateway.replicaCount=5 \
  --reuse-values

# Or edit values file and upgrade
helm upgrade toskamesh ./helm/toskamesh -f values-custom.yaml
```

## Troubleshooting

### Pods Not Starting

```bash
# Check pod status
kubectl get pods -l app.kubernetes.io/name=toskamesh

# Describe pod to see events
kubectl describe pod <pod-name>

# Check logs
kubectl logs <pod-name> --previous  # If pod crashed
kubectl logs <pod-name> -f          # Follow current logs
```

### Common Issues

#### 1. Database Connection Failures

**Symptom**: Services crash with "connection refused" errors

**Solution**:
- Verify PostgreSQL is accessible from the cluster
- Check connection string in values file
- Ensure database exists and credentials are correct

```bash
# Test connection from a pod
kubectl run -it --rm debug --image=postgres:15-alpine -- \
  psql -h postgres-host -U toksa_mesh -d toksa_mesh
```

#### 2. Consul Registration Failures

**Symptom**: Services can't register with Consul

**Solution**:
- Verify Consul address is reachable
- Check Consul is running and healthy
- Review service logs for specific errors

```bash
# Check Consul from inside cluster
kubectl run -it --rm debug --image=curlimages/curl -- \
  curl http://consul:8500/v1/agent/members
```

#### 3. Image Pull Errors

**Symptom**: `ImagePullBackOff` or `ErrImagePull`

**Solution**:
- Ensure images are built and available
- For local development, set `imagePullPolicy: IfNotPresent` or `Never`
- For production, verify registry credentials

```bash
# Create registry secret if needed
kubectl create secret docker-registry regcred \
  --docker-server=your-registry.example.com \
  --docker-username=<username> \
  --docker-password=<password>
```

### Health Check Failures

```bash
# Check service health endpoints
kubectl port-forward svc/toskamesh-gateway 8080:80
curl http://localhost:8080/health
curl http://localhost:8080/health/ready
curl http://localhost:8080/health/live
```

## Upgrading

### Helm Upgrade

```bash
# Upgrade to new version
helm upgrade toskamesh ./helm/toskamesh \
  -f values-custom.yaml \
  --wait

# Rollback if needed
helm rollback toskamesh
```

### Rolling Updates

```bash
# Update image tag
helm upgrade toskamesh ./helm/toskamesh \
  --set gateway.image.tag=1.1.0 \
  --reuse-values

# Watch rollout
kubectl rollout status deployment/toskamesh-gateway
```

## Uninstalling

```bash
# Uninstall the release
helm uninstall toskamesh

# Clean up namespace (if created)
kubectl delete namespace toskamesh-prod
```

## Next Steps

- [Configure Ingress with TLS](ingress-setup.md) (future)
- [Setup Horizontal Pod Autoscaling](hpa-setup.md) (future)
- [Configure Monitoring & Alerting](monitoring-setup.md)
- [Network Policies](network-policies.md) (future)

## Support

For issues and questions:
- GitHub Issues: https://github.com/abstractive-machines/toska-mesh-cs/issues
- Documentation: https://github.com/abstractive-machines/toska-mesh-cs/tree/main/docs

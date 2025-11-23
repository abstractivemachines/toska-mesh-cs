# ToskaMesh EKS Deployment Guide

This guide walks through deploying ToskaMesh to the AWS EKS cluster that was provisioned via Terraform.

## Prerequisites

- AWS CLI configured with appropriate credentials
- kubectl installed
- Helm 3.x installed
- Docker images built locally (all 8 services)
- EKS cluster deployed (via Terraform)

## Step 1: Configure kubectl Access

Configure kubectl to connect to the EKS cluster:

```bash
aws eks update-kubeconfig --name toskamesh-eks --region us-east-1
```

Verify the connection:

```bash
kubectl cluster-info
kubectl get nodes
```

You should see 2 t3.medium nodes in Ready state.

## Step 2: Push Docker Images to ECR

### 2.1 Authenticate Docker to ECR

```bash
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin \
  215958754319.dkr.ecr.us-east-1.amazonaws.com
```

### 2.2 Tag and Push Images

We have a single ECR repository that will hold all service images with different tags:

```bash
# Set variables
ECR_REPO="215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services"
VERSION="v1.0.0"  # or use git commit SHA: $(git rev-parse --short HEAD)

# Gateway
docker tag toskamesh-gateway:latest ${ECR_REPO}:gateway-${VERSION}
docker tag toskamesh-gateway:latest ${ECR_REPO}:gateway-latest
docker push ${ECR_REPO}:gateway-${VERSION}
docker push ${ECR_REPO}:gateway-latest

# Discovery
docker tag toskamesh-discovery:latest ${ECR_REPO}:discovery-${VERSION}
docker tag toskamesh-discovery:latest ${ECR_REPO}:discovery-latest
docker push ${ECR_REPO}:discovery-${VERSION}
docker push ${ECR_REPO}:discovery-latest

# Auth Service
docker tag toskamesh-auth-service:latest ${ECR_REPO}:auth-service-${VERSION}
docker tag toskamesh-auth-service:latest ${ECR_REPO}:auth-service-latest
docker push ${ECR_REPO}:auth-service-${VERSION}
docker push ${ECR_REPO}:auth-service-latest

# Config Service
docker tag toskamesh-config-service:latest ${ECR_REPO}:config-service-${VERSION}
docker tag toskamesh-config-service:latest ${ECR_REPO}:config-service-latest
docker push ${ECR_REPO}:config-service-${VERSION}
docker push ${ECR_REPO}:config-service-latest

# Metrics Service
docker tag toskamesh-metrics-service:latest ${ECR_REPO}:metrics-service-${VERSION}
docker tag toskamesh-metrics-service:latest ${ECR_REPO}:metrics-service-latest
docker push ${ECR_REPO}:metrics-service-${VERSION}
docker push ${ECR_REPO}:metrics-service-latest

# Tracing Service
docker tag toskamesh-tracing-service:latest ${ECR_REPO}:tracing-service-${VERSION}
docker tag toskamesh-tracing-service:latest ${ECR_REPO}:tracing-service-latest
docker push ${ECR_REPO}:tracing-service-${VERSION}
docker push ${ECR_REPO}:tracing-service-latest

# Core
docker tag toskamesh-core:latest ${ECR_REPO}:core-${VERSION}
docker tag toskamesh-core:latest ${ECR_REPO}:core-latest
docker push ${ECR_REPO}:core-${VERSION}
docker push ${ECR_REPO}:core-latest

# Health Monitor
docker tag toskamesh-health-monitor:latest ${ECR_REPO}:health-monitor-${VERSION}
docker tag toskamesh-health-monitor:latest ${ECR_REPO}:health-monitor-latest
docker push ${ECR_REPO}:health-monitor-${VERSION}
docker push ${ECR_REPO}:health-monitor-latest
```

Or use this automated script:

```bash
#!/bin/bash
set -e

ECR_REPO="215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services"
VERSION="${VERSION:-v1.0.0}"

SERVICES=(
  "gateway"
  "discovery"
  "auth-service"
  "config-service"
  "metrics-service"
  "tracing-service"
  "core"
  "health-monitor"
)

for service in "${SERVICES[@]}"; do
  echo "Pushing toskamesh-${service}..."
  docker tag toskamesh-${service}:latest ${ECR_REPO}:${service}-${VERSION}
  docker tag toskamesh-${service}:latest ${ECR_REPO}:${service}-latest
  docker push ${ECR_REPO}:${service}-${VERSION}
  docker push ${ECR_REPO}:${service}-latest
done

echo "All images pushed successfully!"
```

## Step 3: Deploy Dependencies

ToskaMesh requires PostgreSQL, RabbitMQ, Redis, and Consul. You have two options:

### Option A: Deploy In-Cluster (Development/Testing)

Create a namespace for infrastructure:

```bash
kubectl create namespace toskamesh-infra
```

Deploy using Helm charts:

```bash
# PostgreSQL
helm install postgres bitnami/postgresql \
  --namespace toskamesh-infra \
  --set auth.database=toksa_mesh \
  --set auth.username=toksa_mesh \
  --set auth.password=toksa_mesh_password \
  --set primary.persistence.size=10Gi

# RabbitMQ
helm install rabbitmq bitnami/rabbitmq \
  --namespace toskamesh-infra \
  --set auth.username=guest \
  --set auth.password=guest \
  --set persistence.size=8Gi

# Redis
helm install redis bitnami/redis \
  --namespace toskamesh-infra \
  --set auth.enabled=false \
  --set master.persistence.size=8Gi

# Consul
helm install consul hashicorp/consul \
  --namespace toskamesh-infra \
  --set global.name=consul \
  --set server.replicas=1 \
  --set ui.enabled=true
```

### Option B: Use AWS Managed Services (Production)

For production, use managed services:
- **PostgreSQL**: Amazon RDS for PostgreSQL
- **Redis**: Amazon ElastiCache for Redis
- **RabbitMQ**: Amazon MQ for RabbitMQ
- **Consul**: Can use AWS-hosted Consul or keep in-cluster

You'll need to provision these separately and update the values file with their endpoints.

## Step 4: Create EKS-Specific Values File

Create `helm/toskamesh/values-eks.yaml`:

```yaml
# EKS Production values
environment: Production

# Use ECR images
gateway:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: gateway-latest
    pullPolicy: Always
  service:
    type: LoadBalancer
    annotations:
      service.beta.kubernetes.io/aws-load-balancer-type: "nlb"
  resources:
    requests:
      cpu: 200m
      memory: 256Mi
    limits:
      cpu: 1000m
      memory: 1Gi
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 10

discovery:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: discovery-latest
    pullPolicy: Always
  resources:
    requests:
      cpu: 200m
      memory: 256Mi
    limits:
      cpu: 1000m
      memory: 1Gi
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 10

authService:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: auth-service-latest
    pullPolicy: Always
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 5

configService:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: config-service-latest
    pullPolicy: Always
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 5

metricsService:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: metrics-service-latest
    pullPolicy: Always
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 5

tracingService:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: tracing-service-latest
    pullPolicy: Always
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 5

core:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: core-latest
    pullPolicy: Always
  orleans:
    serviceName: "ToskaMesh-Production"
    clusterId: "prod"
  resources:
    requests:
      cpu: 500m
      memory: 512Mi
    limits:
      cpu: 2000m
      memory: 2Gi
  hpa:
    enabled: true
    minReplicas: 3
    maxReplicas: 10

healthMonitor:
  image:
    repository: 215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services
    tag: health-monitor-latest
    pullPolicy: Always
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 5

# External services (Option A: In-cluster)
externalServices:
  consul:
    address: "http://consul-server.toskamesh-infra.svc.cluster.local:8500"
  postgres:
    host: "postgres-postgresql.toskamesh-infra.svc.cluster.local"
    port: 5432
    database: "toksa_mesh"
    username: "toksa_mesh"
    password: "toksa_mesh_password"
  rabbitmq:
    host: "rabbitmq.toskamesh-infra.svc.cluster.local"
    port: 5672
    username: "guest"
    password: "guest"
  redis:
    host: "redis-master.toskamesh-infra.svc.cluster.local"
    port: 6379

# Use KMS for secrets encryption
global:
  meshServiceAuthSecret: "CHANGE-ME-USE-AWS-SECRETS-MANAGER"

# Gateway secrets
gateway:
  secrets:
    jwtSecretKey: "CHANGE-ME-USE-AWS-SECRETS-MANAGER-MIN-32-CHARS"

# Service Account with IRSA (for future AWS service integration)
serviceAccount:
  create: true
  annotations:
    eks.amazonaws.com/role-arn: ""  # Add IRSA role ARN if using AWS services
```

## Step 5: Deploy ToskaMesh with Helm

```bash
# Create namespace
kubectl create namespace toskamesh

# Deploy ToskaMesh
helm install toskamesh ./helm/toskamesh \
  --namespace toskamesh \
  --values helm/toskamesh/values-eks.yaml \
  --timeout 10m
```

Or for an upgrade:

```bash
helm upgrade toskamesh ./helm/toskamesh \
  --namespace toskamesh \
  --values helm/toskamesh/values-eks.yaml \
  --timeout 10m
```

## Step 6: Verify Deployment

### Check Pods

```bash
kubectl get pods -n toskamesh
```

Expected output (with HPA enabled, you'll see 2+ replicas):
```
NAME                                    READY   STATUS    RESTARTS   AGE
toskamesh-gateway-xxxxxxxxx-xxxxx       1/1     Running   0          2m
toskamesh-gateway-xxxxxxxxx-xxxxx       1/1     Running   0          2m
toskamesh-discovery-xxxxxxxxx-xxxxx     1/1     Running   0          2m
toskamesh-discovery-xxxxxxxxx-xxxxx     1/1     Running   0          2m
toskamesh-auth-service-xxxxxxxx-xxxxx   1/1     Running   0          2m
...
```

### Check Services

```bash
kubectl get svc -n toskamesh
```

Get the Gateway LoadBalancer URL:

```bash
kubectl get svc toskamesh-gateway -n toskamesh -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
```

### Check HPA Status

```bash
kubectl get hpa -n toskamesh
```

### View Logs

```bash
# Gateway logs
kubectl logs -n toskamesh -l app.kubernetes.io/name=gateway --tail=100 -f

# Discovery logs
kubectl logs -n toskamesh -l app.kubernetes.io/name=discovery --tail=100 -f

# All services
kubectl logs -n toskamesh -l app.kubernetes.io/instance=toskamesh --tail=50
```

## Step 7: Test the Deployment

### Access the Gateway

```bash
GATEWAY_URL=$(kubectl get svc toskamesh-gateway -n toskamesh -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
echo "Gateway URL: http://${GATEWAY_URL}"

# Test health endpoint
curl http://${GATEWAY_URL}/health

# Test discovery endpoint
curl http://${GATEWAY_URL}/api/discovery/services
```

## Troubleshooting

### Pods Not Starting

Check pod events:
```bash
kubectl describe pod <pod-name> -n toskamesh
```

Check logs:
```bash
kubectl logs <pod-name> -n toskamesh
```

### Image Pull Errors

Verify ECR authentication:
```bash
aws ecr describe-repositories --repository-names toskamesh-eks-services
```

The EKS nodes should have IAM permissions to pull from ECR (granted via the node IAM role).

### Service Discovery Issues

Check if Consul is running:
```bash
kubectl get pods -n toskamesh-infra -l app=consul
kubectl logs -n toskamesh-infra -l app=consul
```

### Database Connection Issues

Check PostgreSQL:
```bash
kubectl get pods -n toskamesh-infra -l app.kubernetes.io/name=postgresql
kubectl logs -n toskamesh-infra -l app.kubernetes.io/name=postgresql
```

Test connection from a pod:
```bash
kubectl run -it --rm debug --image=postgres:14 --restart=Never -n toskamesh -- \
  psql -h postgres-postgresql.toskamesh-infra.svc.cluster.local \
  -U toksa_mesh -d toksa_mesh
```

## Monitoring and Operations

### View Resource Usage

```bash
kubectl top pods -n toskamesh
kubectl top nodes
```

### Scale Services Manually

```bash
kubectl scale deployment toskamesh-gateway -n toskamesh --replicas=5
```

### Update Image

```bash
# After pushing new image to ECR
kubectl set image deployment/toskamesh-gateway \
  gateway=215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services:gateway-v1.1.0 \
  -n toskamesh

# Watch rollout
kubectl rollout status deployment/toskamesh-gateway -n toskamesh
```

### Rollback

```bash
helm rollback toskamesh -n toskamesh
```

## Cleanup

### Uninstall ToskaMesh

```bash
helm uninstall toskamesh -n toskamesh
kubectl delete namespace toskamesh
```

### Uninstall Infrastructure

```bash
helm uninstall postgres -n toskamesh-infra
helm uninstall rabbitmq -n toskamesh-infra
helm uninstall redis -n toskamesh-infra
helm uninstall consul -n toskamesh-infra
kubectl delete namespace toskamesh-infra
```

### Destroy EKS Cluster

```bash
cd deployments/terraform/eks
terraform destroy
```

## Production Considerations

### Security

1. **Secrets Management**: Use AWS Secrets Manager instead of plain text secrets
   ```bash
   # Install External Secrets Operator
   helm repo add external-secrets https://charts.external-secrets.io
   helm install external-secrets external-secrets/external-secrets -n external-secrets-system --create-namespace
   ```

2. **Network Policies**: Enable network policies to restrict pod-to-pod communication

3. **Pod Security Standards**: Enforce PSS (Pod Security Standards)

4. **IRSA**: Use IAM Roles for Service Accounts for AWS service access

### Observability

1. **Install Prometheus + Grafana**:
   ```bash
   helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
   helm install prometheus prometheus-community/kube-prometheus-stack -n monitoring --create-namespace
   ```

2. **Centralized Logging**: Use Fluent Bit to ship logs to CloudWatch Logs

### High Availability

1. Enable multi-AZ node groups in Terraform
2. Configure pod disruption budgets
3. Use topology spread constraints

### Cost Optimization

1. Use Cluster Autoscaler or Karpenter for node scaling
2. Configure appropriate resource requests/limits
3. Use Spot instances for non-critical workloads

## Next Steps

1. Set up CI/CD pipeline to automate deployments
2. Configure custom domain with Route 53 and ACM certificate
3. Enable service mesh (Istio/Linkerd) for advanced traffic management
4. Set up backup and disaster recovery procedures
5. Implement comprehensive monitoring and alerting

---

**Last Updated**: 2025-11-22
**Cluster**: toskamesh-eks (us-east-1)
**Kubernetes Version**: 1.29

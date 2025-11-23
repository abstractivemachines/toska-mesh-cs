# ToskaMesh EKS Quick Start

Quick reference for deploying ToskaMesh to AWS EKS.

## Prerequisites Check

```bash
# Verify tools are installed
aws --version
kubectl version --client
helm version
docker --version

# Verify cluster access
aws eks update-kubeconfig --name toskamesh-eks --region us-east-1
kubectl get nodes
```

## 1. Push Images to ECR (One Command!)

```bash
cd deployments
./push-to-ecr.sh
```

Or manually set version:
```bash
VERSION=v1.0.0 ./push-to-ecr.sh
```

## 2. Deploy Infrastructure (In-Cluster)

```bash
# Add Helm repos
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo add hashicorp https://helm.releases.hashicorp.com
helm repo update

# Create namespace
kubectl create namespace toskamesh-infra

# Deploy all dependencies
helm install postgres bitnami/postgresql \
  --namespace toskamesh-infra \
  --set auth.database=toksa_mesh \
  --set auth.username=toksa_mesh \
  --set auth.password=toksa_mesh_password \
  --set primary.persistence.size=10Gi

helm install rabbitmq bitnami/rabbitmq \
  --namespace toskamesh-infra \
  --set auth.username=guest \
  --set auth.password=guest \
  --set persistence.size=8Gi

helm install redis bitnami/redis \
  --namespace toskamesh-infra \
  --set auth.enabled=false \
  --set master.persistence.size=8Gi

helm install consul hashicorp/consul \
  --namespace toskamesh-infra \
  --set global.name=consul \
  --set server.replicas=1 \
  --set ui.enabled=true

# Wait for all pods to be ready
kubectl wait --for=condition=ready pod -l app.kubernetes.io/instance=postgres -n toskamesh-infra --timeout=300s
kubectl wait --for=condition=ready pod -l app.kubernetes.io/instance=rabbitmq -n toskamesh-infra --timeout=300s
kubectl wait --for=condition=ready pod -l app.kubernetes.io/instance=redis -n toskamesh-infra --timeout=300s
kubectl wait --for=condition=ready pod -l app=consul -n toskamesh-infra --timeout=300s
```

## 3. Deploy ToskaMesh

```bash
# Create namespace
kubectl create namespace toskamesh

# Deploy
helm install toskamesh ./helm/toskamesh \
  --namespace toskamesh \
  --values helm/toskamesh/values-eks.yaml \
  --timeout 10m

# Watch deployment
kubectl get pods -n toskamesh -w
```

## 4. Verify and Test

```bash
# Check all pods are running
kubectl get pods -n toskamesh

# Get gateway URL
GATEWAY_URL=$(kubectl get svc toskamesh-gateway -n toskamesh -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
echo "Gateway: http://${GATEWAY_URL}"

# Test endpoints
curl http://${GATEWAY_URL}/health
curl http://${GATEWAY_URL}/api/discovery/services
```

## Common Operations

### View Logs
```bash
# All services
kubectl logs -n toskamesh -l app.kubernetes.io/instance=toskamesh --tail=50

# Specific service
kubectl logs -n toskamesh -l app.kubernetes.io/name=gateway -f
```

### Scale Service
```bash
kubectl scale deployment toskamesh-gateway -n toskamesh --replicas=5
```

### Update Image
```bash
# Push new image first
VERSION=v1.1.0 ./push-to-ecr.sh

# Update deployment
helm upgrade toskamesh ./helm/toskamesh \
  --namespace toskamesh \
  --values helm/toskamesh/values-eks.yaml \
  --set gateway.image.tag=gateway-v1.1.0
```

### Check Resource Usage
```bash
kubectl top pods -n toskamesh
kubectl top nodes
```

### Restart a Service
```bash
kubectl rollout restart deployment/toskamesh-gateway -n toskamesh
```

## Troubleshooting

### Pods CrashLooping
```bash
kubectl describe pod <pod-name> -n toskamesh
kubectl logs <pod-name> -n toskamesh --previous
```

### Can't Pull Images
```bash
# Verify ECR repo exists
aws ecr describe-repositories --repository-names toskamesh-eks-services

# Check node IAM role has ECR permissions
aws eks describe-nodegroup --cluster-name toskamesh-eks --nodegroup-name default-* --query 'nodegroup.nodeRole'
```

### Database Connection Failed
```bash
# Test from a debug pod
kubectl run -it --rm debug --image=postgres:14 --restart=Never -n toskamesh -- \
  psql -h postgres-postgresql.toskamesh-infra.svc.cluster.local -U toksa_mesh -d toksa_mesh
```

### Service Not Accessible
```bash
# Check service
kubectl get svc toskamesh-gateway -n toskamesh

# Check endpoints
kubectl get endpoints toskamesh-gateway -n toskamesh

# Check security groups allow traffic
aws ec2 describe-security-groups --filters "Name=tag:Name,Values=*toskamesh-eks*"
```

## Cleanup

```bash
# Uninstall ToskaMesh
helm uninstall toskamesh -n toskamesh
kubectl delete namespace toskamesh

# Uninstall infrastructure
helm uninstall postgres rabbitmq redis consul -n toskamesh-infra
kubectl delete namespace toskamesh-infra

# Destroy EKS cluster (if needed)
cd deployments/terraform/eks
terraform destroy
```

## Useful Aliases

Add to your `~/.bashrc` or `~/.zshrc`:

```bash
alias k='kubectl'
alias kgp='kubectl get pods'
alias kgs='kubectl get svc'
alias kl='kubectl logs'
alias kd='kubectl describe'
alias kn='kubectl config set-context --current --namespace'

# ToskaMesh specific
alias ktm='kubectl -n toskamesh'
alias ktmp='kubectl get pods -n toskamesh'
alias ktml='kubectl logs -n toskamesh'
```

## Next Steps

See the full deployment guide for:
- Using AWS managed services (RDS, ElastiCache, MQ)
- Setting up CI/CD pipeline
- Configuring monitoring (Prometheus/Grafana)
- Enabling service mesh (Istio)
- Production security hardening

📚 **Full Guide**: `docs/eks-deployment-guide.md`

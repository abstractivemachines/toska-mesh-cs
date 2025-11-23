# EKS Cost Optimization Guide

## Current vs Optimized Configuration

### Current Configuration
```
Instance Type: t3.medium (2 vCPU, 4 GB RAM)
Capacity Type: ON_DEMAND
Min Nodes: 2
Disk Size: 50 GB
Log Retention: 30 days

Monthly Cost: ~$180-185
```

### Optimized Configuration (Development)
```
Instance Type: t3.small (2 vCPU, 2 GB RAM)
Capacity Type: SPOT
Min Nodes: 1
Disk Size: 20 GB
Log Retention: 7 days

Monthly Cost: ~$119
Savings: $61-66/month (33-36%)
Annual Savings: $732-792
```

## Apply Cost Optimizations

### Option 1: Using tfvars File (Recommended)

```bash
cd deployments/terraform/eks

# Apply optimized configuration
terraform apply -var-file="terraform-dev.tfvars" -auto-approve
```

This will:
1. Replace t3.medium nodes with t3.small SPOT instances
2. Scale down to 1 minimum node
3. Reduce disk size to 20GB
4. Reduce log retention to 7 days

### Option 2: Individual Variable Overrides

```bash
terraform apply \
  -var="node_instance_types=[\"t3.small\"]" \
  -var="node_capacity_type=SPOT" \
  -var="node_min_size=1" \
  -var="node_desired_size=1" \
  -var="node_max_size=3" \
  -var="node_disk_size=20" \
  -var="cloudwatch_log_retention_days=7" \
  -auto-approve
```

### Option 3: Modify variables.tf Defaults

Edit `deployments/terraform/eks/variables.tf` and change defaults:
- `node_instance_types`: `["t3.small"]`
- `node_capacity_type`: `"SPOT"`
- `node_min_size`: `1`
- `node_desired_size`: `1`
- `node_max_size`: `3`
- `node_disk_size`: `20`
- `cloudwatch_log_retention_days`: `7`

Then apply:
```bash
terraform apply -auto-approve
```

## Cost Breakdown

### Monthly Costs (us-east-1)

#### Current Configuration
| Component | Cost |
|-----------|------|
| EKS Control Plane | $73.00 |
| t3.medium × 2 (ON_DEMAND) | $60.74 |
| EBS gp3 (50GB × 2) | $8.00 |
| NAT Gateway | $32.85 |
| CloudWatch Logs (30d) | $5-10 |
| **TOTAL** | **$179.59 - $184.59** |

#### Optimized Configuration
| Component | Cost | Savings |
|-----------|------|---------|
| EKS Control Plane | $73.00 | - |
| t3.small × 1 (SPOT) | $9.13 | -$51.61 |
| EBS gp3 (20GB × 1) | $1.60 | -$6.40 |
| NAT Gateway | $32.85 | - |
| CloudWatch Logs (7d) | $2-3 | -$3-7 |
| **TOTAL** | **$118.58 - $119.58** | **-$61.01 - $66.01** |

## Further Optimization Options

### Maximum Cost Reduction (Aggressive)

**Remove NAT Gateway for Development**

If you can tolerate nodes in public subnets (less secure):

```hcl
# In main.tf, modify VPC module
module "vpc" {
  enable_nat_gateway = false  # Disable NAT Gateway

  # Move nodes to public subnets
  subnet_ids = module.vpc.public_subnets
}
```

**Additional Savings**: $32.85/month
**New Total**: ~$86/month (52% savings)
**Risk**: Nodes have public IPs, less secure

### ARM-based Instances (t4g)

Switch to Graviton2-based instances for 20% additional savings:

```bash
terraform apply -var="node_instance_types=[\"t4g.small\"]"
```

**Requirements**: Multi-arch Docker images (arm64)
**Additional Savings**: ~$1.83/month
**Note**: Requires rebuilding images for ARM architecture

### Scheduled Scaling

For development clusters not needed 24/7:

```bash
# Scale down after hours (example: 6pm - 8am weekdays)
kubectl scale deployment --all --replicas=0 -n toskamesh

# Or use cluster-autoscaler to scale nodes to 0
aws eks update-nodegroup-config \
  --cluster-name toskamesh-eks \
  --nodegroup-name default-* \
  --scaling-config minSize=0,desiredSize=0,maxSize=3
```

**Savings**: ~40-50% if running only 12 hours/day
**Automation**: Can use AWS Lambda + EventBridge for scheduled scaling

## SPOT Instance Considerations

### Interruption Handling

SPOT instances can be interrupted with 2-minute warning. Configure for graceful handling:

1. **Enable Node Termination Handler**:
```bash
helm repo add eks https://aws.github.io/eks-charts
helm install aws-node-termination-handler eks/aws-node-termination-handler \
  --namespace kube-system \
  --set enableSpotInterruptionDraining=true
```

2. **Use Cluster Autoscaler**: Automatically replaces interrupted nodes
3. **Pod Disruption Budgets**: Ensure minimum availability during interruptions

### SPOT Interruption Rates
- t3.small in us-east-1: ~5% interruption rate (historically)
- Most interruptions occur during peak hours
- 2-minute warning allows graceful pod eviction

### Best Practices for SPOT
- Set `node_max_size` to allow scaling beyond min nodes
- Use HPA (Horizontal Pod Autoscaler) for pod-level scaling
- Critical pods should have tolerations for interruptions
- Non-critical workloads are ideal for SPOT

## Resource Right-Sizing

### t3.small Capacity Analysis

Per node capacity:
- **Total**: 2 vCPU, 2 GB RAM
- **Allocatable** (after system pods): ~1.6 vCPU, 1.4 GB RAM

System pod overhead:
- kube-proxy: 100m CPU, 64Mi
- aws-node (VPC CNI): 25m CPU, 64Mi
- coredns (2 replicas): 200m CPU, 140Mi
- **Total System**: 325m CPU, 268Mi RAM

### ToskaMesh Deployment Strategy for t3.small

**Initial Deployment (1 node)**:
- Start with 1 replica per service
- Total requests: ~1600m CPU, ~1.7 GB RAM
- Fits on 1 t3.small with some headroom

**Scaling** (2-3 nodes):
- HPA will scale replicas based on load
- Cluster Autoscaler adds nodes when pods can't schedule
- Max capacity (3 nodes): ~4.8 vCPU, ~4.2 GB RAM

**Recommended values-eks.yaml adjustments**:
```yaml
# Start with 1 replica, HPA to 3
gateway:
  replicaCount: 1
  hpa:
    enabled: true
    minReplicas: 1
    maxReplicas: 3

discovery:
  replicaCount: 1
  hpa:
    enabled: true
    minReplicas: 1
    maxReplicas: 3

# Same pattern for all services...
```

## Monitoring Costs

### Track Actual Costs

```bash
# AWS Cost Explorer (requires AWS Console access)
# Or use AWS CLI:
aws ce get-cost-and-usage \
  --time-period Start=2025-11-01,End=2025-11-30 \
  --granularity MONTHLY \
  --metrics "BlendedCost" \
  --filter file://<(cat <<EOF
{
  "Tags": {
    "Key": "Project",
    "Values": ["toskamesh"]
  }
}
EOF
)
```

### Cost Allocation Tags

Ensure tags are applied for cost tracking:
```hcl
tags = {
  Project    = "toskamesh"
  Environment = "dev"
  CostCenter  = "development"
  ManagedBy   = "terraform"
}
```

## Cost Optimization Checklist

- [x] Use SPOT instances for non-production
- [x] Right-size instance types (t3.small vs t3.medium)
- [x] Reduce minimum node count
- [x] Optimize disk size (20GB vs 50GB)
- [x] Reduce log retention (7 days vs 30 days)
- [x] Use single NAT Gateway
- [ ] Consider removing NAT Gateway for dev (public subnets)
- [ ] Enable Cluster Autoscaler for automatic scaling
- [ ] Use scheduled scaling for dev environments
- [ ] Implement pod resource limits to prevent over-provisioning
- [ ] Review and optimize storage classes
- [ ] Monitor actual usage and adjust accordingly

## Comparison: EKS vs Alternatives

### Local Development (Minikube/Kind)
- **Cost**: $0 (runs on local machine)
- **Use case**: Individual development, testing
- **Limitations**: Not production-like, no AWS integration

### EKS Optimized (This Configuration)
- **Cost**: ~$119/month
- **Use case**: Development environment with AWS integration
- **Benefits**: Production-like, AWS services, team access

### Production EKS
- **Cost**: $300-500+/month
- **Configuration**: ON_DEMAND instances, multi-AZ, larger nodes
- **Required**: High availability, SLA requirements

## Applying Changes

```bash
# 1. Review planned changes
cd deployments/terraform/eks
terraform plan -var-file="terraform-dev.tfvars"

# 2. Apply optimizations
terraform apply -var-file="terraform-dev.tfvars" -auto-approve

# 3. Verify new configuration
aws eks describe-nodegroup --cluster-name toskamesh-eks \
  --nodegroup-name $(aws eks list-nodegroups --cluster-name toskamesh-eks --query 'nodegroups[0]' --output text)

# 4. Monitor for SPOT interruptions
kubectl get events -A --field-selector reason=SpotInterruption -w
```

## Rollback

If issues occur with optimized configuration:

```bash
# Revert to previous configuration
terraform apply \
  -var="node_instance_types=[\"t3.medium\"]" \
  -var="node_capacity_type=ON_DEMAND" \
  -var="node_min_size=2" \
  -var="node_desired_size=2"
```

---

**Last Updated**: 2025-11-22
**Estimated Monthly Savings**: $61-66 (33-36%)
**Annual Savings**: $732-792

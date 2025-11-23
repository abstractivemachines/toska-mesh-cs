# EKS Cost Optimization Summary

## Current State Analysis

**Current Configuration:**
- Instance Type: **t3.medium** (2 vCPU, 4 GB RAM)
- Capacity: **ON_DEMAND**
- Nodes: **2 min, 2 desired, 5 max**
- Disk: **50 GB per node**
- Log Retention: **30 days**

**Current Monthly Cost: ~$180-185**

## Recommended Optimization

**Optimized Configuration:**
- Instance Type: **t3.small** (2 vCPU, 2 GB RAM) ✅
- Capacity: **SPOT** (70% discount) ✅
- Nodes: **1 min, 1 desired, 3 max** ✅
- Disk: **20 GB per node** ✅
- Log Retention: **7 days** ✅

**Optimized Monthly Cost: ~$119**

### Cost Savings
- **Monthly Savings**: $61-66 (33-36% reduction)
- **Annual Savings**: $732-792

## Apply Optimization

### Step 1: Review Changes

```bash
cd deployments/terraform/eks
terraform plan -var-file="terraform-dev.tfvars"
```

### Step 2: Apply Optimization

```bash
terraform apply -var-file="terraform-dev.tfvars"
```

This will:
1. ✅ Replace t3.medium with t3.small SPOT instances
2. ✅ Scale down to 1 minimum node
3. ✅ Reduce disk from 50GB to 20GB
4. ✅ Reduce log retention from 30 to 7 days
5. ✅ Keep single NAT Gateway (already optimized)

### Step 3: Deploy with Optimized Values

Use the cost-optimized Helm values:

```bash
helm install toskamesh ./helm/toskamesh \
  --namespace toskamesh \
  --values helm/toskamesh/values-eks-dev.yaml
```

This uses:
- 1 replica per service initially (instead of 2)
- Reduced resource requests
- HPA enabled to scale based on actual load

## What Happens During Apply

1. **Terraform will**:
   - Create new t3.small SPOT node group
   - Drain and remove t3.medium ON_DEMAND nodes
   - Update CloudWatch log retention
   - Takes ~5-10 minutes

2. **During transition**:
   - Pods will be rescheduled to new nodes
   - Brief interruption possible (plan for off-hours)
   - Node drain is graceful (120s default)

3. **After optimization**:
   - Cluster runs on 1 t3.small SPOT node
   - Autoscales to 2-3 nodes based on load
   - HPA scales pods based on CPU/memory

## Risk Assessment

### Low Risk Changes ✅
- Using SPOT instances (~5% interruption rate)
- Reducing disk size (20GB sufficient for container images)
- Reducing log retention (7 days sufficient for dev)

### Medium Risk Changes ⚠️
- Single node minimum (no HA during node replacement)
- Smaller instance type (tight on resources)

**Mitigation**:
- HPA will scale pods when resources tight
- Cluster Autoscaler will add nodes when pods pending
- SPOT interruptions have 2-min warning for graceful draining
- Can quickly scale back up if needed

## Monitoring After Optimization

### Check Node Status

```bash
kubectl get nodes
aws eks describe-nodegroup --cluster-name toskamesh-eks \
  --nodegroup-name <nodegroup-name>
```

### Monitor Resource Usage

```bash
# Install metrics server first (if needed)
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Check actual usage
kubectl top nodes
kubectl top pods -A
```

### Watch for SPOT Interruptions

```bash
# Install AWS Node Termination Handler
helm repo add eks https://aws.github.io/eks-charts
helm install aws-node-termination-handler eks/aws-node-termination-handler \
  --namespace kube-system \
  --set enableSpotInterruptionDraining=true

# Monitor events
kubectl get events -A --field-selector reason=SpotInterruption -w
```

## Rollback Plan

If optimization causes issues:

```bash
# Quick rollback to t3.medium ON_DEMAND
terraform apply \
  -var="node_instance_types=[\"t3.medium\"]" \
  -var="node_capacity_type=ON_DEMAND" \
  -var="node_min_size=2" \
  -var="node_desired_size=2"
```

## Further Optimization (Optional)

### Option 1: Remove NAT Gateway for Dev
- **Savings**: Additional $33/month
- **Risk**: Nodes in public subnets, less secure
- **Total Cost**: ~$86/month (52% savings)

### Option 2: Scheduled Scaling
- Scale down nodes during non-work hours
- **Savings**: ~40-50% if running 12 hrs/day
- Requires automation (Lambda + EventBridge)

### Option 3: ARM Instances (t4g.small)
- **Savings**: Additional 20% (~$2/month)
- **Requires**: Rebuild Docker images for ARM64

## Files Created

1. **terraform-dev.tfvars** - Optimized Terraform variables
2. **values-eks-dev.yaml** - Optimized Helm values for small nodes
3. **cost-optimization.md** - Detailed cost analysis and guide

## Next Steps

1. ✅ Review this summary
2. ⏸️ Apply Terraform optimization (`terraform apply -var-file="terraform-dev.tfvars"`)
3. ⏸️ Verify cluster is healthy (`kubectl get nodes`)
4. ⏸️ Deploy ToskaMesh with optimized values
5. ⏸️ Monitor resource usage for 1 week
6. ⏸️ Adjust as needed based on actual usage

---

**Status**: Ready to Apply
**Risk Level**: Low-Medium
**Recommended**: Yes (for development environment)
**Production**: Use ON_DEMAND with higher min nodes

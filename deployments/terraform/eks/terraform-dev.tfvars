# Development Environment - Cost Optimized Configuration
# This configuration reduces costs by ~33% while maintaining functionality

# Basic Configuration
aws_region  = "us-east-1"
environment = "dev"

# EKS Configuration
kubernetes_version      = "1.29"
cluster_endpoint_public = true

# VPC Configuration
vpc_cidr           = "10.0.0.0/16"
az_count           = 3
single_nat_gateway = true # Cost savings: single NAT vs 3 NATs

# Node Group Configuration - MINIMUM COST / SCALE-TO-ZERO READY
node_instance_types = ["t3.nano"] # Smallest supported x86 instance for EKS managed node groups
node_capacity_type  = "SPOT"      # Spot instances (saves ~70% on compute)
node_min_size       = 0            # Allow scaling to zero when idle
node_desired_size   = 0            # Start from zero; rely on Cluster Autoscaler to scale up on demand
node_max_size       = 2            # Keep low ceiling for dev workloads
node_disk_size      = 20           # Smaller root volume for cost

# CloudWatch Configuration
cloudwatch_log_retention_days = 7 # Reduced from 30 days (saves ~$3-5/month)

# Tags
tags = {
  CostCenter = "development"
  Optimized  = "true"
}

# Cost Summary:
# - Current Cost: ~$180-185/month
# - Optimized Cost: ~$119/month
# - Monthly Savings: ~$61-66 (33-36% reduction)
# - Annual Savings: ~$732-792
#
# Breakdown:
# - EKS Control Plane: $73/month (fixed)
# - t3.small SPOT (1 node): ~$9/month
# - EBS (20GB): ~$1.60/month
# - NAT Gateway: ~$33/month
# - CloudWatch Logs (7d): ~$2/month
# - Total: ~$119/month

# Add current AWS SSO user to cluster admins
# Note: This is a workaround for SSO authentication
enable_cluster_creator_admin_permissions = true

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

# Node Group Configuration - COST OPTIMIZED
node_instance_types = ["t3.small"] # Reduced from t3.medium (saves ~$25/month for 2 nodes)
node_capacity_type  = "SPOT"       # Spot instances (saves ~70% on compute)
node_min_size       = 1            # Reduced from 2 (saves ~$30/month in base cost)
node_desired_size   = 1            # Start with 1, autoscale as needed
node_max_size       = 3            # Reduced from 5 (development workload)
node_disk_size      = 20           # Reduced from 50 GB (saves ~$2.40/month)

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

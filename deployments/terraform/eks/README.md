# ToskaMesh EKS Terraform Stack

Terraform configuration to provision an AWS EKS cluster (with VPC, managed node group, addons, ECR repo, and KMS key) for deploying ToskaMesh services.

## What it creates
- VPC with public/private subnets, NAT gateway, DNS enabled
- EKS cluster (IRSA enabled) with control-plane logging
- Managed node group with autoscaling settings
- Core EKS addons: VPC CNI (network policy on), CoreDNS, kube-proxy
- ECR repository for ToskaMesh service images
- KMS key/alias for secrets or envelope encryption

## Prerequisites
- Terraform >= 1.6
- AWS credentials configured (profile or env vars)
- kubectl + awscli for post-provision access

## Usage
```bash
cd deployments/terraform/eks

# Inspect/adjust variables in variables.tf or via tfvars
# Authenticate (SSO is preconfigured)
aws sso login

# Initialize with remote state (S3 backend is pre-wired)
terraform init \
  -backend-config="region=us-east-1" \
  -backend-config="bucket=abstractive-machines-terraform-state" \
  -backend-config="key=toska-mesh/eks/terraform.tfstate"

terraform plan -var="cluster_name=toskamesh-eks" -var="environment=dev"
terraform apply -var="cluster_name=toskamesh-eks" -var="environment=dev"

# Configure kubeconfig after apply
aws eks update-kubeconfig --name toskamesh-eks --region <aws_region>
```

Common overrides:
```bash
terraform apply \
  -var="cluster_name=toskamesh-eks-prod" \
  -var="aws_region=us-east-2" \
  -var="kubernetes_version=1.29" \
  -var="node_instance_types=[\"m6i.large\"]" \
  -var="node_min_size=3" -var="node_desired_size=4" -var="node_max_size=8" \
  -var="cluster_endpoint_public=false"
```

## Notes and next steps
- Remote state: S3 backend is preconfigured to use bucket `abstractive-machines-terraform-state` and key `toska-mesh/eks/terraform.tfstate`. Run `terraform init` (or `terraform init -migrate-state`) after AWS SSO login so Terraform can access the bucket.
- For production, consider multiple NAT gateways (`single_nat_gateway=false`) and custom CIDRs.
- Deploy the AWS Load Balancer Controller and Cluster Autoscaler via Helm after kubeconfig is set.
- Use the created ECR repo (`ecr_repository_url` output) for ToskaMesh service images and reference from Helm values.
- Use the KMS key (`kms_key_arn` output) for Secrets encryption or parameter stores if desired.

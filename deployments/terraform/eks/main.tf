terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.66"
    }
  }

  backend "s3" {
    bucket = "abstractive-machines-terraform-state"
    key    = "toska-mesh/eks/terraform.tfstate"
    region = "us-east-1"
  }
}

provider "aws" {
  region = var.aws_region
}

data "aws_region" "current" {}
data "aws_caller_identity" "current" {}
data "aws_availability_zones" "available" {}

locals {
  name = var.cluster_name != "" ? var.cluster_name : "toskamesh-eks"

  az_count = min(length(data.aws_availability_zones.available.names), var.az_count)

  tags = merge(
    {
      "Project"     = "toskamesh"
      "Environment" = var.environment
      "ManagedBy"   = "terraform"
      "Owner"       = data.aws_caller_identity.current.account_id
    },
    var.tags
  )

  public_subnets = var.vpc_public_subnets != null ? var.vpc_public_subnets : [
    cidrsubnet(var.vpc_cidr, 8, 0),
    cidrsubnet(var.vpc_cidr, 8, 1),
    cidrsubnet(var.vpc_cidr, 8, 2),
  ]

  private_subnets = var.vpc_private_subnets != null ? var.vpc_private_subnets : [
    cidrsubnet(var.vpc_cidr, 8, 10),
    cidrsubnet(var.vpc_cidr, 8, 11),
    cidrsubnet(var.vpc_cidr, 8, 12),
  ]
}

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.8"

  name = local.name
  cidr = var.vpc_cidr

  azs                  = slice(data.aws_availability_zones.available.names, 0, local.az_count)
  public_subnets       = local.public_subnets
  private_subnets      = local.private_subnets
  enable_nat_gateway   = true
  single_nat_gateway   = var.single_nat_gateway
  enable_dns_hostnames = true
  enable_dns_support   = true

  public_subnet_tags = {
    "kubernetes.io/role/elb" = "1"
  }

  private_subnet_tags = {
    "kubernetes.io/role/internal-elb" = "1"
  }

  tags = local.tags
}

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 20.13"

  cluster_name                   = local.name
  cluster_version                = var.kubernetes_version
  cluster_endpoint_public_access = var.cluster_endpoint_public

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  enable_irsa                            = true
  cluster_enabled_log_types              = ["api", "audit", "authenticator", "controllerManager", "scheduler"]
  create_cloudwatch_log_group            = true
  cloudwatch_log_group_retention_in_days = var.cloudwatch_log_retention_days

  eks_managed_node_group_defaults = {
    ami_type       = "AL2_x86_64"
    disk_size      = var.node_disk_size
    capacity_type  = var.node_capacity_type
    instance_types = var.node_instance_types
  }

  eks_managed_node_groups = {
    default = {
      min_size     = var.node_min_size
      max_size     = var.node_max_size
      desired_size = var.node_desired_size
      subnet_ids   = module.vpc.private_subnets
    }
  }

  cluster_addons = {
    coredns = {
      most_recent = true
    }
    kube-proxy = {
      most_recent = true
    }
    vpc-cni = {
      most_recent = true
      configuration_values = jsonencode({
        enableNetworkPolicy = true
      })
    }
  }

  tags = local.tags
}

resource "aws_ecr_repository" "services" {
  name                 = "${local.name}-services"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = local.tags
}

resource "aws_kms_key" "secrets" {
  description             = "${local.name} shared secret encryption"
  deletion_window_in_days = 10
  enable_key_rotation     = true
  tags                    = local.tags
}

resource "aws_kms_alias" "secrets" {
  name          = "alias/${local.name}-secrets"
  target_key_id = aws_kms_key.secrets.id
}

output "cluster_name" {
  description = "EKS cluster name"
  value       = module.eks.cluster_name
}

output "cluster_endpoint" {
  description = "EKS cluster endpoint"
  value       = module.eks.cluster_endpoint
}

output "cluster_certificate_authority_data" {
  description = "Base64 encoded CA data for the cluster"
  value       = module.eks.cluster_certificate_authority_data
}

output "region" {
  description = "AWS region"
  value       = data.aws_region.current.name
}

output "ecr_repository_url" {
  description = "ECR repository URI for ToskaMesh services"
  value       = aws_ecr_repository.services.repository_url
}

output "kms_key_arn" {
  description = "KMS key ARN for secrets/encryption"
  value       = aws_kms_key.secrets.arn
}

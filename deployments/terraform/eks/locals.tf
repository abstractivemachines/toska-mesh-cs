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

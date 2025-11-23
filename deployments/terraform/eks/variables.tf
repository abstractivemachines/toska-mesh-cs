variable "aws_region" {
  description = "AWS region for the EKS cluster"
  type        = string
  default     = "us-east-1"
}

variable "cluster_name" {
  description = "EKS cluster name override (defaults to toskamesh-eks)"
  type        = string
  default     = ""
}

variable "environment" {
  description = "Deployment environment tag"
  type        = string
  default     = "dev"
}

variable "kubernetes_version" {
  description = "EKS/Kubernetes version"
  type        = string
  default     = "1.30"
}

variable "cluster_endpoint_public" {
  description = "Expose EKS API endpoint publicly (true) or privately (false)"
  type        = bool
  default     = true
}

variable "vpc_cidr" {
  description = "VPC CIDR block"
  type        = string
  default     = "10.0.0.0/16"
}

variable "az_count" {
  description = "Number of availability zones to span (<= available AZs)"
  type        = number
  default     = 3
}

variable "vpc_public_subnets" {
  description = "Optional override for public subnets"
  type        = list(string)
  default     = null
}

variable "vpc_private_subnets" {
  description = "Optional override for private subnets"
  type        = list(string)
  default     = null
}

variable "single_nat_gateway" {
  description = "Use a single shared NAT gateway instead of one per AZ"
  type        = bool
  default     = true
}

variable "node_instance_types" {
  description = "EKS worker node instance types"
  type        = list(string)
  default     = ["t3.medium"]
}

variable "node_capacity_type" {
  description = "EKS node capacity type (ON_DEMAND or SPOT)"
  type        = string
  default     = "ON_DEMAND"
}

variable "node_min_size" {
  description = "Minimum nodes in the default node group"
  type        = number
  default     = 2
}

variable "node_max_size" {
  description = "Maximum nodes in the default node group"
  type        = number
  default     = 5
}

variable "node_desired_size" {
  description = "Desired nodes in the default node group"
  type        = number
  default     = 2
}

variable "node_disk_size" {
  description = "Worker node root volume size (GiB)"
  type        = number
  default     = 50
}

variable "cloudwatch_log_retention_days" {
  description = "Retention for EKS control plane logs"
  type        = number
  default     = 30
}

variable "tags" {
  description = "Additional resource tags"
  type        = map(string)
  default     = {}
}

variable "enable_cluster_creator_admin_permissions" {
  description = "Grant the cluster creator (current caller) admin permissions automatically"
  type        = bool
  default     = true
}

variable "access_entries" {
  description = "Map of access entries to add to the cluster for kubectl access"
  type = map(object({
    principal_arn = string
    type          = optional(string, "STANDARD")
    policy_associations = optional(map(object({
      policy_arn = string
    })), {})
  }))
  default = {}
}

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

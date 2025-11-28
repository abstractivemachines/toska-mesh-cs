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

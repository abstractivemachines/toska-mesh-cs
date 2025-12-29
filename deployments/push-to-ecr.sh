#!/bin/bash
set -e

# Configuration
ECR_REPO="${ECR_REPO:-215958754319.dkr.ecr.us-east-1.amazonaws.com/toskamesh-eks-services}"
VERSION="${VERSION:-$(git rev-parse --short HEAD 2>/dev/null || echo 'v1.0.0')}"
AWS_REGION="${AWS_REGION:-us-east-1}"

# Color output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}ToskaMesh ECR Image Push Script${NC}"
echo "================================"
echo "ECR Repository: $ECR_REPO"
echo "Version Tag: $VERSION"
echo "AWS Region: $AWS_REGION"
echo ""

# Authenticate to ECR
echo -e "${YELLOW}Authenticating to ECR...${NC}"
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin \
  $(echo $ECR_REPO | cut -d'/' -f1)
echo -e "${GREEN}✓ Authenticated${NC}"
echo ""

# Services to push
SERVICES=(
  "gateway"
  "dashboard"
  "discovery"
  "auth-service"
  "config-service"
  "metrics-service"
  "tracing-service"
  "core"
  "health-monitor"
)

# Function to push a service
push_service() {
  local service=$1
  echo -e "${YELLOW}Pushing toskamesh-${service}...${NC}"

  # Tag with version and latest
  docker tag toskamesh-${service}:latest ${ECR_REPO}:${service}-${VERSION}
  docker tag toskamesh-${service}:latest ${ECR_REPO}:${service}-latest

  # Push both tags
  docker push ${ECR_REPO}:${service}-${VERSION}
  docker push ${ECR_REPO}:${service}-latest

  echo -e "${GREEN}✓ Pushed ${service}${NC}"
}

# Push all services
for service in "${SERVICES[@]}"; do
  push_service "$service"
  echo ""
done

echo -e "${GREEN}================================${NC}"
echo -e "${GREEN}All images pushed successfully!${NC}"
echo ""
echo "Images:"
for service in "${SERVICES[@]}"; do
  echo "  - ${ECR_REPO}:${service}-${VERSION}"
  echo "  - ${ECR_REPO}:${service}-latest"
done
echo ""
echo "You can now deploy with:"
echo "  helm install toskamesh ./helm/toskamesh \\"
echo "    --namespace toskamesh \\"
echo "    --values helm/toskamesh/values-eks.yaml"

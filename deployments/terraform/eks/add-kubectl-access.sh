#!/bin/bash
# Add kubectl access for the current AWS IAM principal to EKS cluster
# This script helps grant EKS access when using AWS SSO or IAM users

set -e

CLUSTER_NAME="${1:-toskamesh-eks}"
REGION="${AWS_REGION:-us-east-1}"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Adding kubectl access to EKS cluster: $CLUSTER_NAME"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Get current caller identity
CALLER_IDENTITY=$(aws sts get-caller-identity)
ACCOUNT_ID=$(echo $CALLER_IDENTITY | jq -r '.Account')
CALLER_ARN=$(echo $CALLER_IDENTITY | jq -r '.Arn')
USER_ID=$(echo $CALLER_IDENTITY | jq -r '.UserId')

echo ""
echo "Current AWS Identity:"
echo "  Account: $ACCOUNT_ID"
echo "  ARN: $CALLER_ARN"
echo "  User ID: $USER_ID"
echo ""

# Determine if this is an SSO role or IAM user
if [[ $CALLER_ARN == *":assumed-role/"* ]]; then
    PRINCIPAL_TYPE="SSO/Assumed Role"
    # Extract role name from assumed-role ARN
    # Format: arn:aws:sts::ACCOUNT:assumed-role/ROLE_NAME/SESSION_NAME
    ROLE_NAME=$(echo $CALLER_ARN | cut -d'/' -f2)
    PRINCIPAL_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"
    echo "⚠️  Detected: $PRINCIPAL_TYPE"
    echo "  Role ARN: $PRINCIPAL_ARN"
    echo ""
    echo "Note: SSO roles cannot be added via CLI. Options:"
    echo "  1. Use AWS Console to add access entry (recommended for SSO)"
    echo "  2. Create a dedicated IAM user for kubectl access"
    echo "  3. Run 'terraform apply' which grants access to cluster creator"
    echo ""
    read -p "Try to add this role anyway? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Exiting. Please use AWS Console or create an IAM user."
        exit 0
    fi
elif [[ $CALLER_ARN == *":user/"* ]]; then
    PRINCIPAL_TYPE="IAM User"
    PRINCIPAL_ARN=$CALLER_ARN
    echo "✓ Detected: $PRINCIPAL_TYPE"
    echo "  This can be added to EKS access entries"
else
    echo "⚠️  Unknown principal type: $CALLER_ARN"
    echo "  This may not work with EKS access entries"
    PRINCIPAL_ARN=$CALLER_ARN
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Creating EKS Access Entry..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Try to create access entry
if aws eks create-access-entry \
    --cluster-name "$CLUSTER_NAME" \
    --principal-arn "$PRINCIPAL_ARN" \
    --type STANDARD \
    --region "$REGION" 2>/dev/null; then
    echo "✓ Access entry created successfully"
else
    EXIT_CODE=$?
    if [ $EXIT_CODE -eq 254 ]; then
        echo "⚠️  Access entry already exists (this is OK)"
    else
        echo "✗ Failed to create access entry"
        echo ""
        echo "This usually means:"
        echo "  - SSO roles can't be added via CLI (use AWS Console)"
        echo "  - Access entry already exists (check AWS Console)"
        echo "  - Insufficient permissions to create access entry"
        echo ""
        echo "To add via AWS Console:"
        echo "  1. Go to: https://console.aws.amazon.com/eks/home?region=$REGION#/clusters/$CLUSTER_NAME"
        echo "  2. Click 'Access' tab"
        echo "  3. Click 'Create access entry'"
        echo "  4. Add ARN: $PRINCIPAL_ARN"
        echo "  5. Associate with policy: AmazonEKSClusterAdminPolicy"
        exit 1
    fi
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Associating with EKS Cluster Admin Policy..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Associate with cluster admin policy
if aws eks associate-access-policy \
    --cluster-name "$CLUSTER_NAME" \
    --principal-arn "$PRINCIPAL_ARN" \
    --policy-arn "arn:aws:eks::aws:cluster-access-policy/AmazonEKSClusterAdminPolicy" \
    --access-scope type=cluster \
    --region "$REGION" 2>/dev/null; then
    echo "✓ Policy association successful"
else
    echo "⚠️  Policy may already be associated (this is OK)"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Updating kubeconfig..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

aws eks update-kubeconfig \
    --name "$CLUSTER_NAME" \
    --region "$REGION"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Testing kubectl access..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if kubectl get nodes 2>/dev/null; then
    echo ""
    echo "✓ SUCCESS! kubectl access is working"
else
    echo ""
    echo "✗ kubectl access still not working"
    echo ""
    echo "This usually means the access entry hasn't propagated yet."
    echo "Wait 30-60 seconds and try: kubectl get nodes"
    echo ""
    echo "If still failing, you may need to:"
    echo "  1. Use AWS Console to add access entry manually"
    echo "  2. Create a dedicated IAM user for kubectl"
    echo "  3. Run 'terraform apply -var-file=terraform-dev.tfvars'"
fi

echo ""

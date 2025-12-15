#!/usr/bin/env bash
set -euo pipefail

# Creates/updates the toskamesh-gateway-tls secret with server/client PFX and CA bundle.
# Usage:
#   ./tools/create-gateway-tls-secret.sh \
#     --namespace toskamesh \
#     --server-pfx ./gateway-server.pfx --server-pass changeit \
#     --client-pfx ./gateway-client.pfx --client-pass changeit \
#     --ca-crt ./ca.crt \
#     [--kestrel-url https://+:8443] [--kubeconfig ~/.kube/config] [--context my-cluster] [--dry-run]

NAMESPACE="default"
SECRET_NAME="toskamesh-gateway-tls"
SERVER_PFX=""
SERVER_PASS=""
CLIENT_PFX=""
CLIENT_PASS=""
CA_CRT=""
KESTREL_URL="https://+:8443"
SERVER_PFX_PATH="/etc/tls/gateway-server.pfx"
KUBECONFIG_PATH=""
KUBE_CONTEXT=""
DRY_RUN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --namespace) NAMESPACE="$2"; shift 2 ;;
    --server-pfx) SERVER_PFX="$2"; shift 2 ;;
    --server-pass) SERVER_PASS="$2"; shift 2 ;;
    --client-pfx) CLIENT_PFX="$2"; shift 2 ;;
    --client-pass) CLIENT_PASS="$2"; shift 2 ;;
    --ca-crt) CA_CRT="$2"; shift 2 ;;
    --kestrel-url) KESTREL_URL="$2"; shift 2 ;;
    --kubeconfig) KUBECONFIG_PATH="$2"; shift 2 ;;
    --context) KUBE_CONTEXT="$2"; shift 2 ;;
    --dry-run) DRY_RUN=true; shift ;;
    *) echo "Unknown arg: $1" >&2; exit 1 ;;
  esac
done

if [[ -z "$SERVER_PFX" || -z "$SERVER_PASS" || -z "$CLIENT_PFX" || -z "$CLIENT_PASS" || -z "$CA_CRT" ]]; then
  echo "Missing required arguments. See header for usage." >&2
  exit 1
fi

for file in "$SERVER_PFX" "$CLIENT_PFX" "$CA_CRT"; do
  if [[ ! -f "$file" ]]; then
    echo "File not found: $file" >&2
    exit 1
  fi
done

if ! command -v kubectl >/dev/null 2>&1; then
  echo "kubectl is required on PATH." >&2
  exit 1
fi

KUBECTL_CMD=(kubectl)
if [[ -n "$KUBECONFIG_PATH" ]]; then
  KUBECTL_CMD+=("--kubeconfig" "$KUBECONFIG_PATH")
fi
if [[ -n "$KUBE_CONTEXT" ]]; then
  KUBECTL_CMD+=("--context" "$KUBE_CONTEXT")
fi

"${KUBECTL_CMD[@]}" create secret generic "$SECRET_NAME" \
  --namespace "$NAMESPACE" \
  --from-file=gateway-server.pfx="$SERVER_PFX" \
  --from-file=gateway-client.pfx="$CLIENT_PFX" \
  --from-file=ca.crt="$CA_CRT" \
  --from-literal=gateway_server_pfx_password="$SERVER_PASS" \
  --from-literal=gateway_client_pfx_password="$CLIENT_PASS" \
  --from-literal=gateway_server_pfx_path="$SERVER_PFX_PATH" \
  --from-literal=kestrel_https_url="$KESTREL_URL" \
  --dry-run=client -o yaml | {
    if [[ "$DRY_RUN" == true ]]; then
      cat
    else
      "${KUBECTL_CMD[@]}" apply -f -
    fi
  }

if [[ "$DRY_RUN" == true ]]; then
  echo "Generated secret manifest for $SECRET_NAME in namespace $NAMESPACE (dry-run)."
else
  echo "Updated secret $SECRET_NAME in namespace $NAMESPACE with server/client PFX and CA."
fi

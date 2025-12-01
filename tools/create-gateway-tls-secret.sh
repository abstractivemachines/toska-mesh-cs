#!/usr/bin/env bash
set -euo pipefail

# Creates/updates the toskamesh-gateway-tls secret with server/client PFX and CA bundle.
# Usage:
#   ./tools/create-gateway-tls-secret.sh \
#     --namespace toskamesh \
#     --server-pfx ./gateway-server.pfx --server-pass changeit \
#     --client-pfx ./gateway-client.pfx --client-pass changeit \
#     --ca-crt ./ca.crt \
#     [--kestrel-url https://+:8443]

NAMESPACE="default"
SECRET_NAME="toskamesh-gateway-tls"
SERVER_PFX=""
SERVER_PASS=""
CLIENT_PFX=""
CLIENT_PASS=""
CA_CRT=""
KESTREL_URL="https://+:8443"
SERVER_PFX_PATH="/etc/tls/gateway-server.pfx"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --namespace) NAMESPACE="$2"; shift 2 ;;
    --server-pfx) SERVER_PFX="$2"; shift 2 ;;
    --server-pass) SERVER_PASS="$2"; shift 2 ;;
    --client-pfx) CLIENT_PFX="$2"; shift 2 ;;
    --client-pass) CLIENT_PASS="$2"; shift 2 ;;
    --ca-crt) CA_CRT="$2"; shift 2 ;;
    --kestrel-url) KESTREL_URL="$2"; shift 2 ;;
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

kubectl create secret generic "$SECRET_NAME" \
  --namespace "$NAMESPACE" \
  --from-file=gateway-server.pfx="$SERVER_PFX" \
  --from-file=gateway-client.pfx="$CLIENT_PFX" \
  --from-file=ca.crt="$CA_CRT" \
  --from-literal=gateway_server_pfx_password="$SERVER_PASS" \
  --from-literal=gateway_client_pfx_password="$CLIENT_PASS" \
  --from-literal=gateway_server_pfx_path="$SERVER_PFX_PATH" \
  --from-literal=kestrel_https_url="$KESTREL_URL" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "Updated secret $SECRET_NAME in namespace $NAMESPACE with server/client PFX and CA."

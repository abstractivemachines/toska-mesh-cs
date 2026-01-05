#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${MESH_RPC_DEMO_BASE_URL:-http://talos:30080/api/mesh-rpc-a}"
VALUE="${MESH_RPC_DEMO_VALUE:-hello}"

curl -s -S --max-time 10 "${BASE_URL%/}/start?value=${VALUE}"

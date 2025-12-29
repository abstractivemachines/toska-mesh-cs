#!/bin/sh
# Inject runtime configuration into config.js
# This script runs as part of nginx docker-entrypoint.d

set -e

GATEWAY_BASE_URL=${DASHBOARD_GATEWAY_BASE_URL:-}

cat > /usr/share/nginx/html/config.js <<CONFIG
window.__DASHBOARD_CONFIG__ = {
  gatewayBaseUrl: "${GATEWAY_BASE_URL}"
};
CONFIG

echo "Dashboard config injected: gatewayBaseUrl=${GATEWAY_BASE_URL:-'(empty)'}"

#!/bin/sh
set -e

GATEWAY_BASE_URL=${DASHBOARD_GATEWAY_BASE_URL:-}

cat > /usr/share/nginx/html/config.js <<CONFIG
window.__DASHBOARD_CONFIG__ = {
  gatewayBaseUrl: "${GATEWAY_BASE_URL}"
};
CONFIG

exec nginx -g 'daemon off;'

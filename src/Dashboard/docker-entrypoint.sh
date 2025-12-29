#!/bin/sh
# Generate config.js from environment variable
cat > /usr/share/nginx/html/config.js << EOF
window.__DASHBOARD_CONFIG__ = {
  gatewayBaseUrl: "${DASHBOARD_GATEWAY_BASE_URL:-}"
};
EOF

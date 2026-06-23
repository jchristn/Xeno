#!/usr/bin/env sh
set -eu
cat > /usr/share/nginx/html/config.js <<EOF
window.__XENO_CONFIG__ = {
  serverUrl: "${XENO_SERVER_URL:-http://localhost:9000}"
};
EOF
nginx -g 'daemon off;'

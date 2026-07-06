#!/usr/bin/env sh
set -eu
if [ -z "${XENO_SERVER_URL:-}" ] || [ "${XENO_SERVER_URL}" = "http://localhost:9000" ]; then
  cat > /usr/share/nginx/html/config.js <<EOF
window.__XENO_CONFIG__ = {
  serverUrl: window.location.protocol + "//" + window.location.hostname + ":9000"
};
EOF
else
  cat > /usr/share/nginx/html/config.js <<EOF
window.__XENO_CONFIG__ = {
  serverUrl: "${XENO_SERVER_URL}"
};
EOF
fi
nginx -g 'daemon off;'

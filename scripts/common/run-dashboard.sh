#!/usr/bin/env bash
# Run the Xeno server web dashboard (dashboard/, Vite dev server) in the foreground at http://localhost:9100.
# Use it to sign in to a server, manage tenants, users, and access keys, and watch clients and requests.
# This is the server's dashboard; the client's local dashboard is served by the client worker
# (http://127.0.0.1:9343 when enabled in Settings).
#
# Usage: run-dashboard.sh
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

for arg in "$@"; do
    case "${arg}" in
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[run-dashboard] Unknown argument: ${arg} (this script takes no arguments)" >&2; exit 2 ;;
    esac
done
command -v npm >/dev/null 2>&1 || { echo "[run-dashboard] npm is required (install Node.js 20 or later)." >&2; exit 1; }
cd "${XENO_REPO_ROOT}/dashboard"
if [ ! -d node_modules ]; then
    echo "[run-dashboard] Installing dashboard packages..."
    npm install
fi

echo "[run-dashboard] Starting the dashboard at http://localhost:9100 (Ctrl+C to stop)..."
exec npm run dev

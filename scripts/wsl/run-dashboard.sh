#!/usr/bin/env bash
# Run the Xeno server web dashboard (dashboard/, Vite dev server) in the foreground at http://localhost:9100.
# Use it to sign in to a server, manage tenants, users, and access keys, and watch clients and requests.
# This is the server's dashboard; the client's local dashboard is served by the client worker
# (http://127.0.0.1:9343 when enabled in Settings).
# WSL 1 version: needs Node.js inside this distro (a Windows npm is not used). WSL 1 shares localhost with Windows, so Windows can open it at the same URL.
#
# Usage: run-dashboard.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-dashboard] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"


for arg in "$@"; do
    case "$arg" in
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[run-dashboard] Unknown argument: $arg (this script takes no arguments)" >&2; exit 2 ;;
    esac
done
wsl_require_cmd npm "Install Node.js 20 or later inside WSL (https://nodejs.org/en/download, or your distro's packages)."

cd "$XENO_REPO_ROOT/dashboard"
if [ ! -d node_modules ]; then
    echo "[run-dashboard] Installing dashboard packages..."
    npm install
fi

echo "[run-dashboard] Starting the dashboard at http://localhost:9100 (Ctrl+C to stop)..."
exec npm run dev

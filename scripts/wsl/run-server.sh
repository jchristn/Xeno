#!/usr/bin/env bash
# Build and run a local Xeno server (src/Xeno.Server) from source in the foreground. Ctrl+C stops it.
# It runs in artifacts/local-server (ignored by git): xeno.json there starts as a copy of the repository's xeno.json,
# with the SQLite database in data/ and logs in logs/. REST and WebSocket listen on port 9000. Delete
# artifacts/local-server to start over with an empty server.
# WSL 1 version: the server runs in this distro. WSL 1 shares localhost with Windows, so Windows can open it at the same URL.
#
# Usage: run-server.sh [settings-file]
#   run-server.sh                          use artifacts/local-server/xeno.json
#   run-server.sh /path/to/other/xeno.json use another settings file (absolute path)
#
# Point a client at it with: run-desktop.sh --server http://localhost:9000 (or run-tui.sh, or xeno server use ...).
# run-local.sh starts this server, the web dashboard, and a client app together.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-server] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"

case "${1:-}" in
    -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
esac
if [ $# -gt 1 ]; then
    echo "[run-server] Unexpected argument: $2 (this script takes at most a settings file)" >&2
    exit 2
fi
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
SETTINGS="${1:-xeno.json}"
xeno_prepare_local_server

echo "[run-server] Building Xeno.Server (${XENO_CONFIGURATION})..."
xeno_quiet_build "$XENO_SERVER_PROJECT" -c "$XENO_CONFIGURATION"

cd "$XENO_LOCAL_SERVER_DIR"
echo "[run-server] Starting the Xeno server in $XENO_LOCAL_SERVER_DIR with $SETTINGS (Ctrl+C to stop)..."
exec dotnet run --project "$XENO_SERVER_PROJECT" -c "$XENO_CONFIGURATION" --no-build -- "$SETTINGS"

#!/usr/bin/env bash
# Build and run the Xeno client worker from source in the foreground (xeno connect --foreground). The terminal shows
# connection changes, warnings, and errors; the full log goes to the log file (xeno logs). Ctrl+C stops it. The worker connects to the server in client.json and serves the local
# web dashboard (http://127.0.0.1:9343 by default). It also starts the Xeno Agent tray app when there is a desktop
# session (XENO_AGENT_AUTOSTART=0 turns that off).
#
# Usage: run-client.sh [--server <cloud|url|host[:port]>]
#   --server   point this client at a server first (saved to client.json), for example http://localhost:9000
# Use XENO_CLIENT_HOME to pick the client home directory.
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

SERVER=""
while [ $# -gt 0 ]; do
    case "$1" in
        --server)
            [ $# -ge 2 ] && [ -n "$2" ] || { echo "[run-client] --server requires a value." >&2; exit 2; }
            SERVER="$2"; shift 2 ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[run-client] Unknown argument: $1" >&2; exit 2 ;;
    esac
done
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

xeno_build_apps run-client
XENO="$(xeno_exe xeno)"
if [ -n "${SERVER}" ]; then
    "${XENO}" server use "${SERVER}"
fi

echo "[run-client] Stopping any background client so this one can run in the foreground..."
"${XENO}" stop >/dev/null 2>&1 || true

echo "[run-client] Starting the client worker in the foreground (Ctrl+C to stop)..."
exec "${XENO}" connect --foreground

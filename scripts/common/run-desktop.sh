#!/usr/bin/env bash
# Build and run the Xeno desktop app (xeno-desktop) from source. It starts the Xeno Agent tray app too, unless
# XENO_AGENT_AUTOSTART=0.
#
# Usage: run-desktop.sh [--server <cloud|url|host[:port]>] [--home <dir>] [--page <page>]
#   run-desktop.sh                                 use the server in client.json (the Xeno cloud by default)
#   run-desktop.sh --server http://localhost:9000  point this client at a local server and save it
#   run-desktop.sh --server cloud                  point back at the Xeno cloud
#   run-desktop.sh --home ~/.xeno-local            use a separate client home (one per server is easiest)
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
case "${1:-}" in
    -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
esac
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

xeno_build_apps run-desktop
echo "[run-desktop] Starting xeno-desktop..."
exec "$(xeno_exe xeno-desktop)" "$@"

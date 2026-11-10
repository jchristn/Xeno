#!/usr/bin/env bash
# Build and run the Xeno terminal app (xeno tui) from source in this terminal.
#
# Usage: run-tui.sh [--server <cloud|url|host[:port]>] [--home <dir>] [--page <page>]
#   run-tui.sh                                 use the server in client.json (the Xeno cloud by default)
#   run-tui.sh --server http://localhost:9000  point this client at a local server and save it
#   run-tui.sh --server cloud                  point back at the Xeno cloud
#   run-tui.sh --home ~/.xeno-local --page models
# Keys: F10 menu, Ctrl+K command palette, F1 help, Alt+1..9 pages, Ctrl+Q quit.
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
case "${1:-}" in
    -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
esac
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

xeno_build_apps run-tui
echo "[run-tui] Starting xeno tui..."
exec "$(xeno_exe xeno)" tui "$@"

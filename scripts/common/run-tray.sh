#!/usr/bin/env bash
# Build and run the Xeno Agent tray app (xeno-tray) from source in the foreground. Its menu opens the desktop app,
# the terminal app, and the local web dashboard, and starts and stops the client.
#
# Usage: run-tray.sh [--home <dir>]
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
case "${1:-}" in
    -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
esac
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

xeno_build_apps run-tray
echo "[run-tray] Starting xeno-tray (look for Xeno Agent in the menu bar or notification area)..."
exec "$(xeno_exe xeno-tray)" "$@"

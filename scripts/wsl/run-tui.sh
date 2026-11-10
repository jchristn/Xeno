#!/usr/bin/env bash
# Build and run the Xeno terminal app (xeno tui) from source in this terminal.
# WSL 1 version: uses the Linux dotnet and the client home in this distro (~/.xeno), not the Windows one.
#
# Usage: run-tui.sh [--server <cloud|url|host[:port]>] [--home <dir>] [--page <page>]
#   run-tui.sh                                 use the server in client.json (the Xeno cloud by default)
#   run-tui.sh --server http://localhost:9000  point this client at a local server and save it
#   run-tui.sh --server cloud                  point back at the Xeno cloud
#   run-tui.sh --home ~/.xeno-local --page models
# Keys: F10 menu, Ctrl+K command palette, F1 help, Alt+1..9 pages, Ctrl+Q quit.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-tui] ERROR: $*" >&2; exit 1; }

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
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_check_libs warning "The Xeno desktop app (which the terminal app opens)" "$WSL_AVALONIA_LIBS"
wsl_check_clipboard "the Xeno terminal app"

xeno_build_apps run-tui
echo "[run-tui] Starting xeno tui..."
# Give the app the Windows PATH entries back (clip.exe, explorer.exe, wslview) and BROWSER=wslview.
wsl_prepare_app_env
exec "$(xeno_exe xeno)" tui "$@"

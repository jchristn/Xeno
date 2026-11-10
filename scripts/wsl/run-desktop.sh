#!/usr/bin/env bash
# Build and run the Xeno desktop app (xeno-desktop) from source.
# WSL 1 version: WSL 1 has no WSLg, so this needs an X server on Windows with DISPLAY set. The desktop app
# does not start the tray agent (there is no notification area) unless XENO_AGENT_AUTOSTART=1.
#
# Usage: run-desktop.sh [--server <cloud|url|host[:port]>] [--home <dir>] [--page <page>]
#   run-desktop.sh                                 use the server in client.json (the Xeno cloud by default)
#   run-desktop.sh --server http://localhost:9000  point this client at a local server and save it
#   run-desktop.sh --server cloud                  point back at the Xeno cloud
#   run-desktop.sh --home ~/.xeno-local            use a separate client home (one per server is easiest)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-desktop] ERROR: $*" >&2; exit 1; }

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
if [ -z "${DISPLAY:-}" ]; then
    die "WSL 1 has no WSLg and DISPLAY is not set, so the desktop app cannot be shown. Run scripts\\windows\\run-desktop.bat from Windows, use scripts/wsl/run-tui.sh for the terminal app, move this distro to WSL 2 (wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2) and use scripts/wsl2, or start an X server on Windows and set DISPLAY (for example export DISPLAY=localhost:0)."
fi
wsl_check_libs error "The Xeno desktop app" "$WSL_AVALONIA_LIBS"
# WSLg has no notification area, so the desktop app does not start the tray agent unless XENO_AGENT_AUTOSTART is set.
export XENO_AGENT_AUTOSTART="${XENO_AGENT_AUTOSTART:-0}"

xeno_build_apps run-desktop
echo "[run-desktop] Starting xeno-desktop..."
# Give the app the Windows PATH entries back (clip.exe, explorer.exe, wslview) and BROWSER=wslview.
wsl_prepare_app_env
exec "$(xeno_exe xeno-desktop)" "$@"

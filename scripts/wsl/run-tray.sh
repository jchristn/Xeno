#!/usr/bin/env bash
# Build and run the Xeno Agent tray app (xeno-tray) from source in the foreground.
# WSL 1 version: refuses to run, because WSL 1 has no WSLg and no notification area, so the tray icon
# cannot appear. Use run-desktop.sh or run-tui.sh here, or scripts\windows\run-tray.bat for a Windows client.
# XENO_WSL_FORCE_TRAY=1 starts it anyway (for example under your own X desktop that has a tray).
#
# Usage: run-tray.sh [--home <dir>]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-tray] ERROR: $*" >&2; exit 1; }

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
if [ "${XENO_WSL_FORCE_TRAY:-0}" != 1 ]; then
    die "WSL 1 has no WSLg and no notification area, so the Xeno Agent tray icon cannot appear. Use scripts/wsl/run-desktop.sh or scripts/wsl/run-tui.sh in this distro, or scripts\\windows\\run-tray.bat for a Windows client. Set XENO_WSL_FORCE_TRAY=1 to start it anyway (for example with your own X desktop that has a tray)."
fi
if [ -z "${DISPLAY:-}" ]; then
    die "WSL 1 has no WSLg and DISPLAY is not set, so the tray agent cannot be shown. Run scripts\\windows\\run-tray.bat from Windows, use scripts/wsl/run-tui.sh for the terminal app, move this distro to WSL 2 (wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2) and use scripts/wsl2, or start an X server on Windows and set DISPLAY (for example export DISPLAY=localhost:0)."
fi
wsl_check_libs error "The Xeno Agent tray app" "$WSL_AVALONIA_LIBS"

xeno_build_apps run-tray
echo "[run-tray] Starting xeno-tray..."
# Give the app the Windows PATH entries back (clip.exe, explorer.exe, wslview) and BROWSER=wslview.
wsl_prepare_app_env
exec "$(xeno_exe xeno-tray)" "$@"

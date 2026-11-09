#!/usr/bin/env sh
# Builds and runs the Xeno Agent tray app (xeno-tray). WSL 2 counterpart of scripts/linux/run-tray.sh.
# WSL 2 needs WSLg; WSL 1 cannot run GUI apps.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl2   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

if [ -z "${WAYLAND_DISPLAY:-}" ] && [ -z "${DISPLAY:-}" ]; then
    wsl_die "The tray agent needs WSLg, but no display is set (DISPLAY/WAYLAND_DISPLAY). WSLg ships with Windows 11 and the Store version of WSL: run 'wsl --update' from Windows, then 'wsl --shutdown'. Or use scripts/windows/run-tray.bat, or scripts/wsl2/run-tui.sh for the terminal app."
fi
wsl_log "Note: WSLg has no notification area, so the Xeno Agent tray icon may not appear. The terminal app (run-tui.sh) and the Windows tray agent (scripts/windows/run-tray.bat) are alternatives."
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_run "$SCRIPT_DIR/../linux/run-tray.sh" "$@"

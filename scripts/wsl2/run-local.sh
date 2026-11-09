#!/usr/bin/env sh
# Runs a local server and web dashboard, then a client app connected to them. WSL 2 counterpart of scripts/linux/run-local.sh.
# The desktop app (default) and the tray agent need WSLg.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl2   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_require_cmd curl "Install curl inside WSL (for example: sudo apt install curl)."

case "${1:-desktop}" in
    desktop|tray|-*)
        case "${1:-}" in -h|--help) wsl_run "$SCRIPT_DIR/../linux/run-local.sh" "$@" ;; esac
        if [ -z "${WAYLAND_DISPLAY:-}" ] && [ -z "${DISPLAY:-}" ]; then
            wsl_die "The desktop app and the tray agent need WSLg (run 'wsl --update' from Windows). Or use: scripts/wsl2/run-local.sh tui (or client)."
        fi
        [ "${1:-desktop}" = tray ] && wsl_log "Note: WSLg has no notification area, so the Xeno Agent tray icon may not appear. The terminal app (run-tui.sh) and the Windows tray agent (scripts/windows/run-tray.bat) are alternatives."
        ;;
esac
wsl_run "$SCRIPT_DIR/../linux/run-local.sh" "$@"

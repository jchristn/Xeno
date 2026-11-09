#!/usr/bin/env sh
# Runs a local server and web dashboard, then a client app connected to them. WSL 1 counterpart of scripts/linux/run-local.sh.
# The default app is the desktop app, which WSL 1 cannot show: pass tui or client.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
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
        wsl_die "WSL 1 cannot run the desktop app or the tray agent. Use: scripts/wsl/run-local.sh tui (or client)." ;;
esac
wsl_run "$SCRIPT_DIR/../linux/run-local.sh" "$@"

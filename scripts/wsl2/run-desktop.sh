#!/usr/bin/env sh
# Builds and runs the desktop app (xeno-desktop). WSL 2 counterpart of scripts/linux/run-desktop.sh.
# WSL 2 shows it through WSLg; WSL 1 cannot run GUI apps.
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
    wsl_die "The desktop app needs WSLg, but no display is set (DISPLAY/WAYLAND_DISPLAY). WSLg ships with Windows 11 and the Store version of WSL: run 'wsl --update' from Windows, then 'wsl --shutdown'. Or use scripts/windows/run-desktop.bat, or scripts/wsl2/run-tui.sh for the terminal app."
fi
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"

# A server running on Windows is not at localhost under WSL 2 NAT networking; rewrite --server when needed.
_wsl_argc=$#
_wsl_next=0
for _wsl_arg in "$@"; do
    if [ "$_wsl_next" = 1 ]; then
        _wsl_arg="$(wsl_resolve_local_url "$_wsl_arg")"
        _wsl_next=0
    fi
    [ "$_wsl_arg" = --server ] && _wsl_next=1
    set -- "$@" "$_wsl_arg"
done
shift "$_wsl_argc"
wsl_run "$SCRIPT_DIR/../linux/run-desktop.sh" "$@"

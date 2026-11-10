#!/usr/bin/env bash
# Remove the Xeno installation from this WSL distro: stops the client worker, quits the desktop app and the tray
# agent, uninstalls the xeno global tool, and deletes ~/.local/share/xeno/app and the launcher entries. Succeeds
# when nothing is installed. Your client configuration and history in ~/.xeno are kept; use factory-reset.sh to
# delete them.
# WSL 1 version: removes the install in this distro only; use scripts\windows\remove.bat for Windows.
#
# Usage: remove.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[remove] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"


for arg in "$@"; do
    case "$arg" in
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[remove] Unknown argument: $arg (this script takes no arguments)" >&2; exit 2 ;;
    esac
done
wsl_require_cmd pkill "Install procps inside WSL (for example: sudo apt install procps)."

# Stops the worker and the apps, then uninstalls the xeno tool and the apps in this distro.
do_remove() {
    local cli target applications
    cli="$(xeno_any_cli)"
    if [ -n "$cli" ]; then
        echo "[remove] Stopping the client worker if it is running..."
        "$cli" stop >/dev/null 2>&1 || true
    fi

    echo "[remove] Quitting the desktop app and tray agent if they are running..."
    pkill -x xeno-desktop 2>/dev/null || true
    pkill -x xeno-tray 2>/dev/null || true

    echo "[remove] Uninstalling the xeno global tool if present..."
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "[remove] WARNING: dotnet was not found, so the xeno tool uninstall was skipped. Install the .NET SDK inside WSL and run remove again to uninstall it." >&2
    elif xeno_tool_installed; then
        dotnet tool uninstall --global xeno >/dev/null 2>&1 || true
        dotnet tool uninstall --global Xeno.Client >/dev/null 2>&1 || true
        if xeno_tool_installed; then
            echo "[remove] ERROR: could not uninstall the xeno global tool. Close every xeno process (xeno tui, xeno connect) and try again." >&2
            exit 1
        fi
    else
        echo "[remove] The xeno global tool is not installed; nothing to uninstall."
    fi

    target="$(xeno_linux_install_dir)"
    applications="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
    if [ -d "$target" ]; then
        echo "[remove] Removing $target"
        rm -rf "$target"
    fi
    rm -f "$applications/xeno.desktop" "$applications/xeno-agent.desktop"
    echo "[remove] Completed. Client data in $(xeno_client_home) was kept."
}

do_remove

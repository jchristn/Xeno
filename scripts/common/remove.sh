#!/usr/bin/env bash
# Remove the Xeno installation: stops the client worker, quits the desktop app and the tray agent, uninstalls the
# xeno global tool, and deletes the installed apps (~/Applications/Xeno.app on macOS, ~/.local/share/xeno/app and
# the launcher entries on Linux). Your client configuration and history in ~/.xeno are kept; use factory-reset.sh
# to delete them.
#
# Usage: remove.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

CLI="$(xeno_any_cli)"
if [ -n "${CLI}" ]; then
    echo "[remove] Stopping the client worker if it is running..."
    "${CLI}" stop >/dev/null 2>&1 || true
fi

echo "[remove] Quitting the desktop app and tray agent if they are running..."
pkill -x xeno-desktop 2>/dev/null || true
pkill -x xeno-tray 2>/dev/null || true

echo "[remove] Uninstalling the xeno global tool if present..."
dotnet tool uninstall --global xeno >/dev/null 2>&1 || true
dotnet tool uninstall --global Xeno.Client >/dev/null 2>&1 || true

if [ "$(uname -s)" = "Darwin" ]; then
    if [ -d "${HOME}/Applications/Xeno.app" ]; then
        echo "[remove] Removing ${HOME}/Applications/Xeno.app"
        rm -rf "${HOME}/Applications/Xeno.app"
    fi
else
    TARGET="$(xeno_linux_install_dir)"
    APPLICATIONS="${XDG_DATA_HOME:-${HOME}/.local/share}/applications"
    if [ -d "${TARGET}" ]; then
        echo "[remove] Removing ${TARGET}"
        rm -rf "${TARGET}"
    fi
    rm -f "${APPLICATIONS}/xeno.desktop" "${APPLICATIONS}/xeno-agent.desktop"
fi

echo "[remove] Completed. Client data in $(xeno_client_home) was kept."

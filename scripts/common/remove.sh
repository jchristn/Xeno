#!/usr/bin/env bash
# Remove the Xeno installation: stops the client worker, quits the desktop app and the tray agent, uninstalls the
# xeno global tool, and deletes the installed apps (~/Applications/Xeno.app on macOS, ~/.local/share/xeno/app and
# the launcher entries on Linux), plus the sign-in entry run-at-startup.sh wrote. Your client configuration and history in ~/.xeno are kept; use factory-reset.sh
# to delete them.
#
# Usage: remove.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

for arg in "$@"; do
    case "${arg}" in
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[remove] Unknown argument: ${arg} (this script takes no arguments)" >&2; exit 2 ;;
    esac
done

CLI="$(xeno_any_cli)"
if [ -n "${CLI}" ]; then
    echo "[remove] Stopping the client worker if it is running..."
    "${CLI}" stop >/dev/null 2>&1 || true
fi

echo "[remove] Quitting the desktop app and tray agent if they are running..."
pkill -x xeno-desktop 2>/dev/null || true
pkill -x xeno-tray 2>/dev/null || true

echo "[remove] Uninstalling the xeno global tool if present..."
if ! command -v dotnet >/dev/null 2>&1; then
    echo "[remove] WARNING: dotnet was not found, so the xeno tool uninstall was skipped. Install the .NET SDK and run remove again to uninstall it." >&2
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

STARTUP_ENTRY="$(xeno_startup_entry)"
if [ -f "${STARTUP_ENTRY}" ]; then
    echo "[remove] Removing the sign-in entry ${STARTUP_ENTRY}"
    rm -f "${STARTUP_ENTRY}"
fi

echo "[remove] Completed. Client data in $(xeno_client_home) was kept."

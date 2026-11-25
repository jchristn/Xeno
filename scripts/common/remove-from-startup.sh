#!/usr/bin/env bash
# Stop starting Xeno Agent when you sign in: deletes the entry run-at-startup.sh wrote
# (macOS: ~/Library/LaunchAgents/ai.xenocloud.xeno.scripts-agent.plist; Linux: ~/.config/autostart/xeno-tray.desktop).
# A running agent keeps running; quit it from its menu. Succeeds when nothing was registered. Sign-in items added by
# the Xeno installers are not changed.
#
# Usage: remove-from-startup.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"
die() { echo "[remove-from-startup] ERROR: $*" >&2; exit 1; }

for arg in "$@"; do
    case "${arg}" in
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[remove-from-startup] Unknown argument: ${arg} (this script takes no arguments)" >&2; exit 2 ;;
    esac
done

ENTRY="$(xeno_startup_entry)"
if [ -f "${ENTRY}" ]; then
    rm -f "${ENTRY}"
    echo "[remove-from-startup] Xeno Agent no longer starts when you sign in. Removed ${ENTRY}"
else
    echo "[remove-from-startup] Nothing to remove: Xeno Agent was not set to start at sign-in by run-at-startup.sh."
fi

if [ "$(uname -s)" = "Darwin" ]; then
    if [ -f "/Library/LaunchAgents/ai.xenocloud.xeno.agent.plist" ]; then
        echo "[remove-from-startup] Note: the Xeno installer's sign-in item (/Library/LaunchAgents/ai.xenocloud.xeno.agent.plist) still starts Xeno Agent. Turn it off in System Settings > General > Login Items."
    fi
elif [ -f "/etc/xdg/autostart/xeno-tray.desktop" ]; then
    echo "[remove-from-startup] Note: the Xeno package's autostart entry (/etc/xdg/autostart/xeno-tray.desktop) still starts Xeno Agent. Turn it off in your desktop's startup applications settings."
fi

#!/usr/bin/env bash
# Update the installed Xeno from this checkout: stops the client worker, reinstalls the xeno tool and the apps, and
# starts the worker again if it was running. Your client configuration in ~/.xeno is kept.
#
# Usage: update.sh [--no-apps] [--no-dashboard]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

WAS_RUNNING=0
if [ -n "$(xeno_worker_pid)" ]; then
    WAS_RUNNING=1
fi

"${SCRIPT_DIR}/reinstall.sh" "$@"

if [ "${WAS_RUNNING}" -eq 1 ]; then
    echo
    echo "[update] Starting the client worker again..."
    "$(xeno_any_cli)" start
fi

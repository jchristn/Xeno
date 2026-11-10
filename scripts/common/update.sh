#!/usr/bin/env bash
# Update the installed Xeno from this checkout: stops the client worker, reinstalls the xeno tool and the apps, and
# starts the worker again if it was running. Your client configuration in ~/.xeno is kept.
#
# Usage: update.sh [--no-apps] [--no-dashboard]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

INSTALL_APPS=1
for arg in "$@"; do
    case "${arg}" in
        --no-apps) INSTALL_APPS=0 ;;
        --no-dashboard) ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[update] Unknown argument: ${arg}" >&2; exit 2 ;;
    esac
done

# Before removing anything, so a package that cannot be installed leaves the current install in place.
if [ "${INSTALL_APPS}" -eq 1 ]; then
    native_install_deps "Xeno" icu gui clipboard
else
    native_install_deps "Xeno" icu clipboard
fi

WAS_RUNNING=0
if [ -n "$(xeno_worker_pid)" ]; then
    WAS_RUNNING=1
fi

"${SCRIPT_DIR}/reinstall.sh" "$@"

if [ "${WAS_RUNNING}" -eq 1 ]; then
    echo
    echo "[update] Starting the client worker again..."
    CLI="$(xeno_any_cli)"
    [ -n "${CLI}" ] || { echo "[update] ERROR: xeno was not found after the reinstall; start the worker with: xeno start" >&2; exit 1; }
    "${CLI}" start
fi

#!/usr/bin/env bash
# Stop starting the Xeno client in this distro when you sign in to Windows: deletes the Windows Startup launcher
# run-at-startup.sh wrote. A running client keeps running; stop it with xeno stop. Succeeds when nothing was registered.
# WSL 1 version: changes this distro only; use scripts\windows\remove-from-startup.bat for the Windows client.
#
# Usage: remove-from-startup.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[remove-from-startup] ERROR: $*" >&2; exit 1; }

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
        *) echo "[remove-from-startup] Unknown argument: $arg (this script takes no arguments)" >&2; exit 2 ;;
    esac
done

if wsl_startup_remove xeno-client; then
    echo "[remove-from-startup] The Xeno client in this distro ($WSL_DISTRO_NAME) no longer starts when you sign in to Windows."
else
    echo "[remove-from-startup] Nothing to remove: the Xeno client in this distro was not set to start at sign-in."
fi

#!/usr/bin/env bash
# Reset the Xeno client in this distro to a fresh state. Stops the client worker, the desktop app, and the tray agent,
# then deletes the client's local history database, logs, state, and app preferences in the client home
# (~/.xeno, or XENO_CLIENT_HOME). By default client.json (server, sign-in, providers, models, settings) is kept;
# pass --all to delete the whole client home, which signs this distro out and points it back at the Xeno cloud.
# WSL 2 version: resets the client in this distro only; use scripts\windows\factory-reset.bat for the Windows client.
#
# Usage: factory-reset.sh [-y|--yes|--force] [--all]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[factory-reset] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"

FORCE=0
WIPE_ALL=0
for arg in "$@"; do
    case "$arg" in
        -y|--yes|--force) FORCE=1 ;;
        --all) WIPE_ALL=1 ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[factory-reset] Unknown argument: $arg" >&2
           echo "Usage: factory-reset.sh [-y|--yes|--force] [--all]" >&2
           exit 2 ;;
    esac
done
wsl_require_cmd pkill "Install procps inside WSL (for example: sudo apt install procps)."

HOME_DIR="$(xeno_client_home)"
if [ ! -d "$HOME_DIR" ]; then
    echo "[factory-reset] Nothing to reset: $HOME_DIR does not exist."
    exit 0
fi

if [ "$FORCE" -ne 1 ]; then
    echo
    echo "WARNING: this permanently deletes Xeno client data in:"
    echo "    $HOME_DIR"
    echo "  - the local history database (requests, host and provider telemetry)"
    echo "  - logs, client state, and desktop and terminal app preferences"
    if [ "$WIPE_ALL" -eq 1 ]; then
        echo "  - --all: ALSO client.json (server, sign-in, providers, models, and settings)"
    fi
    echo
    printf 'Type YES to continue: '
    read -r CONFIRM || CONFIRM=""
    if [ "$CONFIRM" != "YES" ]; then
        echo "[factory-reset] Aborted."
        exit 1
    fi
fi

echo "[factory-reset] Stopping the client worker, desktop app, and tray agent..."
CLI="$(xeno_any_cli)"
if [ -n "$CLI" ]; then
    "$CLI" stop --force >/dev/null 2>&1 || true
fi
pkill -x xeno-desktop 2>/dev/null || true
pkill -x xeno-tray 2>/dev/null || true

if [ -n "$(xeno_worker_pid)" ]; then
    echo "ERROR: the client worker is still running. Stop it (xeno stop --force) and run factory-reset again." >&2
    exit 1
fi

if [ "$WIPE_ALL" -eq 1 ]; then
    echo "[factory-reset] Deleting $HOME_DIR..."
    rm -rf "$HOME_DIR"
else
    echo "[factory-reset] Deleting history, logs, state, and preferences (keeping client.json)..."
    find "$HOME_DIR" -mindepth 1 -maxdepth 1 ! -name client.json -exec rm -rf {} +
fi

echo "[factory-reset] Done."

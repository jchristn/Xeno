#!/usr/bin/env bash
# Run a complete local Xeno setup for development and testing:
#   1. builds the server and the client apps,
#   2. starts a local Xeno server in the background (artifacts/local-server, port 9000; see run-server.sh) and waits
#      until it is healthy,
#   3. starts the server web dashboard at http://localhost:9100 in the background (skip with XENO_SKIP_DASHBOARD=1),
#   4. runs a client app in the foreground, pointed at the local server.
# The client uses its own home directory (XENO_LOCAL_HOME, default ~/.xeno-local), so your real ~/.xeno and its
# Xeno cloud sign-in are never touched. When the app exits (or you press Ctrl+C), the server and dashboard stop.
# Background output goes to artifacts/local-server/logs/run-local-server.log and run-local-dashboard.log.
# WSL 1 version: everything runs in this distro. The desktop app needs an X server on Windows with DISPLAY set; the tray agent is not available.
# WSL 1 shares localhost with Windows, so Windows can open it at the same URL.
#
# Usage: run-local.sh [desktop|tui|tray|client] [app arguments...]
#   run-local.sh               desktop app (default)
#   run-local.sh tui           terminal app in this terminal
#   run-local.sh tray          tray agent (refused under WSL unless XENO_WSL_FORCE_TRAY=1)
#   run-local.sh client        client worker in the foreground (xeno connect --foreground)
#   run-local.sh tui --page server
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-local] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"

APP="desktop"
case "${1:-}" in
    desktop|tui|tray|client) APP="$1"; shift ;;
    -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
esac
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_require_cmd curl "Install curl inside WSL (for example: sudo apt install curl); it checks that the local server is healthy."
case "$APP" in
    tray)
        if [ "${XENO_WSL_FORCE_TRAY:-0}" != 1 ]; then
            die "WSL 1 has no WSLg and no notification area, so the Xeno Agent tray icon cannot appear. Use scripts/wsl/run-desktop.sh or scripts/wsl/run-tui.sh in this distro, or scripts\\windows\\run-tray.bat for a Windows client. Set XENO_WSL_FORCE_TRAY=1 to start it anyway (for example with your own X desktop that has a tray)."
        fi
        if [ -z "${DISPLAY:-}" ]; then
            die "WSL 1 has no WSLg and DISPLAY is not set, so the tray agent cannot be shown. Run scripts\\windows\\run-local.bat from Windows, use scripts/wsl/run-tui.sh for the terminal app, move this distro to WSL 2 (wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2) and use scripts/wsl2, or start an X server on Windows and set DISPLAY (for example export DISPLAY=localhost:0)."
        fi
        ;;
    desktop)
        if [ -z "${DISPLAY:-}" ]; then
            die "WSL 1 has no WSLg and DISPLAY is not set, so the desktop app cannot be shown. Run scripts\\windows\\run-local.bat from Windows, use scripts/wsl/run-tui.sh for the terminal app, move this distro to WSL 2 (wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2) and use scripts/wsl2, or start an X server on Windows and set DISPLAY (for example export DISPLAY=localhost:0)."
        fi
        ;;
esac
# WSLg has no notification area, so the desktop app does not start the tray agent unless XENO_AGENT_AUTOSTART is set.
export XENO_AGENT_AUTOSTART="${XENO_AGENT_AUTOSTART:-0}"

HEALTH_URL="${XENO_LOCAL_SERVER%/}/"
export XENO_CLIENT_HOME="$XENO_LOCAL_HOME"
mkdir -p "$XENO_CLIENT_HOME"
xeno_prepare_local_server
LOGS="$XENO_LOCAL_SERVER_DIR/logs"

echo "[run-local] Building Xeno.Server (${XENO_CONFIGURATION})..."
xeno_quiet_build "$XENO_SERVER_PROJECT" -c "$XENO_CONFIGURATION"
xeno_build_apps run-local

PIDS=""
cleanup() {
    local pid
    for pid in $PIDS; do
        xeno_kill_tree "$pid"
    done
}
trap cleanup EXIT
trap 'exit 130' INT TERM

if curl -sf -o /dev/null "$HEALTH_URL"; then
    echo "[run-local] A Xeno server is already answering at $XENO_LOCAL_SERVER; using it."
else
    echo "[run-local] Starting the Xeno server (log: $LOGS/run-local-server.log)..."
    (cd "$XENO_LOCAL_SERVER_DIR" && exec dotnet run --project "$XENO_SERVER_PROJECT" -c "$XENO_CONFIGURATION" --no-build -- xeno.json) \
        >"$LOGS/run-local-server.log" 2>&1 &
    SERVER_PID=$!
    PIDS="$PIDS $SERVER_PID"
    echo "[run-local] Waiting for $HEALTH_URL..."
    if ! xeno_wait_for_server "$HEALTH_URL" "$SERVER_PID"; then
        echo "[run-local] ERROR: the server did not become healthy. Last log lines:" >&2
        tail -n 30 "$LOGS/run-local-server.log" >&2 || true
        exit 1
    fi
    echo "[run-local] Server healthy at $XENO_LOCAL_SERVER."
fi

if [ "${XENO_SKIP_DASHBOARD:-0}" != "1" ]; then
    if command -v npm >/dev/null 2>&1; then
        if [ ! -d "$XENO_REPO_ROOT/dashboard/node_modules" ]; then
            echo "[run-local] Installing dashboard packages..."
            (cd "$XENO_REPO_ROOT/dashboard" && npm install --silent)
        fi
        echo "[run-local] Starting the web dashboard at http://localhost:9100 (log: $LOGS/run-local-dashboard.log)..."
        (cd "$XENO_REPO_ROOT/dashboard" && exec npm run dev) >"$LOGS/run-local-dashboard.log" 2>&1 &
        PIDS="$PIDS $!"
    else
        echo "[run-local] npm was not found inside WSL, so the web dashboard is skipped. Install Node.js 20 or later inside WSL to run it." >&2
    fi
fi

XENO="$(xeno_exe xeno)"
"$XENO" server use "$XENO_LOCAL_SERVER" >/dev/null
echo "[run-local] Client home: $XENO_CLIENT_HOME (server $XENO_LOCAL_SERVER)"
echo "[run-local] Sign in from the General page with admin@xeno / password."

case "$APP" in
    desktop) "$(xeno_exe xeno-desktop)" "$@" ;;
    tui) "$XENO" tui "$@" ;;
    tray) "$(xeno_exe xeno-tray)" "$@" ;;
    client) "$XENO" connect --foreground "$@" ;;
esac

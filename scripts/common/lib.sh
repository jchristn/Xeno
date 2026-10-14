#!/usr/bin/env bash
# Shared settings and helpers for the Xeno scripts. Source this file; do not run it.
#
# Environment variables:
#   XENO_CONFIGURATION   Build configuration for the run-* scripts (default Debug; install uses Release).
#   XENO_FRAMEWORK       Target framework (default net10.0).
#   XENO_CLIENT_HOME     Client home directory used by xeno, xeno-desktop, xeno-tray, and xeno tui (default ~/.xeno).
#   XENO_LOCAL_SERVER    URL of the local Xeno server started by run-server.sh and run-local.sh
#                        (default http://localhost:9000, matching Rest.Port in xeno.json).
#   XENO_LOCAL_HOME      Client home run-local.sh uses, so local testing never touches ~/.xeno (default ~/.xeno-local).

XENO_REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
XENO_CONFIGURATION="${XENO_CONFIGURATION:-Debug}"
XENO_FRAMEWORK="${XENO_FRAMEWORK:-net10.0}"
XENO_LOCAL_SERVER="${XENO_LOCAL_SERVER:-http://localhost:9000}"
XENO_LOCAL_HOME="${XENO_LOCAL_HOME:-${HOME}/.xeno-local}"
XENO_DESKTOP_PROJECT="${XENO_REPO_ROOT}/src/Xeno.Desktop/Xeno.Desktop.csproj"
XENO_CLIENT_PROJECT="${XENO_REPO_ROOT}/src/Xeno.Client/Xeno.Client.csproj"
XENO_SERVER_PROJECT="${XENO_REPO_ROOT}/src/Xeno.Server/Xeno.Server.csproj"
XENO_TOOL_PACKAGES="${XENO_REPO_ROOT}/artifacts/tools"
XENO_LOCAL_SERVER_DIR="${XENO_REPO_ROOT}/artifacts/local-server"

# Prepare the local server's working folder (artifacts/local-server, ignored by git): settings, SQLite database, and
# logs live there, so running a local server never changes the tracked xeno.json. The settings are copied from the
# repository's xeno.json the first time; delete the folder to start over.
xeno_prepare_local_server() {
    mkdir -p "${XENO_LOCAL_SERVER_DIR}/data" "${XENO_LOCAL_SERVER_DIR}/logs"
    if [ ! -f "${XENO_LOCAL_SERVER_DIR}/xeno.json" ]; then
        cp "${XENO_REPO_ROOT}/xeno.json" "${XENO_LOCAL_SERVER_DIR}/xeno.json"
    fi
}

# Folder holding xeno, xeno-desktop, and xeno-tray after xeno_build_apps. The three executables live side by side so
# each app finds the others.
xeno_app_dir() {
    echo "${XENO_REPO_ROOT}/src/Xeno.Desktop/bin/${XENO_CONFIGURATION}/${XENO_FRAMEWORK}"
}

# Run dotnet build quietly: the output is shown only when the build fails. Usage: xeno_quiet_build <dotnet build args...>
xeno_quiet_build() {
    local log
    log="$(mktemp "${TMPDIR:-/tmp}/xeno-build.XXXXXX")"
    if ! dotnet build "$@" --nologo >"${log}" 2>&1; then
        cat "${log}" >&2
        rm -f "${log}"
        return 1
    fi
    rm -f "${log}"
}

# Build the desktop app, which also builds xeno (CLI, worker, and terminal app) and xeno-tray into the same folder.
xeno_build_apps() {
    echo "[$1] Building xeno, xeno-desktop, and xeno-tray (${XENO_CONFIGURATION}, ${XENO_FRAMEWORK})..."
    xeno_quiet_build "${XENO_DESKTOP_PROJECT}" -c "${XENO_CONFIGURATION}" -f "${XENO_FRAMEWORK}"
}

# Path to a built executable: xeno, xeno-desktop, or xeno-tray.
xeno_exe() {
    echo "$(xeno_app_dir)/$1"
}

# Directory the install scripts put xeno-desktop and xeno-tray in (macOS uses ~/Applications/Xeno.app instead).
# The apps search this directory, so xeno tui can open the desktop app.
xeno_linux_install_dir() {
    echo "${XDG_DATA_HOME:-${HOME}/.local/share}/xeno/app"
}

# The client home directory currently in effect.
xeno_client_home() {
    echo "${XENO_CLIENT_HOME:-${HOME}/.xeno}"
}

# Run the installed xeno tool if there is one, otherwise the build in this checkout. Prints nothing when neither exists.
xeno_any_cli() {
    if command -v xeno >/dev/null 2>&1; then
        command -v xeno
    elif [ -x "${HOME}/.dotnet/tools/xeno" ]; then
        echo "${HOME}/.dotnet/tools/xeno"
    elif [ -x "$(xeno_exe xeno)" ]; then
        xeno_exe xeno
    fi
}

# Print the process id of the running client worker for this client home, or nothing when it is not running.
xeno_worker_pid() {
    local cli pid
    cli="$(xeno_any_cli)"
    [ -n "${cli}" ] || return 0
    pid="$("${cli}" status --json 2>/dev/null | sed -n 's/.*"processId": *\([0-9][0-9]*\).*/\1/p' | head -1)"
    if [ -n "${pid}" ] && [ "${pid}" != "0" ] && kill -0 "${pid}" 2>/dev/null; then
        echo "${pid}"
    fi
}

# Stop a process and everything it started (dotnet and npm start child processes).
xeno_kill_tree() {
    local child
    for child in $(pgrep -P "$1" 2>/dev/null); do
        xeno_kill_tree "${child}"
    done
    kill "$1" 2>/dev/null || true
}

# Wait for the local Xeno server to answer on its health endpoint. Returns 0 when healthy.
xeno_wait_for_server() {
    local url="$1" pid="${2:-}"
    for _ in $(seq 1 45); do
        if [ -n "${pid}" ] && ! kill -0 "${pid}" 2>/dev/null; then
            return 1
        fi
        if curl -sf -o /dev/null "${url}"; then
            return 0
        fi
        sleep 1
    done
    return 1
}

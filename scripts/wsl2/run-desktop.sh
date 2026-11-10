#!/usr/bin/env bash
# Build and run the Xeno desktop app (xeno-desktop) from source.
# WSL 2 version: needs WSLg. WSLg has no notification area, so the desktop app does not start the tray
# agent unless XENO_AGENT_AUTOSTART=1. A --server URL on localhost that only a server on Windows answers
# is pointed at the Windows host.
#
# Usage: run-desktop.sh [--server <cloud|url|host[:port]>] [--home <dir>] [--page <page>]
#   run-desktop.sh                                 use the server in client.json (the Xeno cloud by default)
#   run-desktop.sh --server http://localhost:9000  point this client at a local server and save it
#   run-desktop.sh --server cloud                  point back at the Xeno cloud
#   run-desktop.sh --home ~/.xeno-local            use a separate client home (one per server is easiest)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-desktop] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"

case "${1:-}" in
    -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
esac
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
if [ -z "${DISPLAY:-}" ]; then
    die "DISPLAY is not set. The desktop app uses X11 and needs WSLg, which sets DISPLAY (Windows 11, or Windows 10 with the Microsoft Store WSL). From Windows run wsl --update, then wsl --shutdown, and reopen this shell. Or run scripts\\windows\\run-desktop.bat from Windows, or scripts/wsl2/run-tui.sh for the terminal app."
fi
wsl_check_libs error "The Xeno desktop app" "$WSL_AVALONIA_LIBS"
# WSLg has no notification area, so the desktop app does not start the tray agent unless XENO_AGENT_AUTOSTART is set.
export XENO_AGENT_AUTOSTART="${XENO_AGENT_AUTOSTART:-0}"

# WSL 2 NAT networking: localhost inside the distro is not Windows. When a --server URL on localhost gets no answer
# here but a server on Windows answers, use the Windows host address instead (WSL_NO_HOST_REWRITE=1 turns this off).
resolve_server() {
    local url="$1" probe resolved
    case "$url" in
        localhost|localhost:*|localhost/*|127.0.0.1|127.0.0.1:*|127.0.0.1/*) probe="http://$url" ;;
        *) probe="$url" ;;
    esac
    resolved="$(wsl_resolve_local_url "$probe")"
    if [ "$resolved" = "$probe" ]; then
        printf '%s\n' "$url"
        return 0
    fi
    echo "[run-desktop] Note: the client saves $resolved. The Windows host address can change when Windows restarts; enable networkingMode=mirrored in .wslconfig for a stable localhost." >&2
    printf '%s\n' "$resolved"
}

ARGS=()
while [ $# -gt 0 ]; do
    if [ "$1" = --server ] && [ $# -ge 2 ]; then
        ARGS+=("$1" "$(resolve_server "$2")")
        shift 2
    else
        ARGS+=("$1")
        shift
    fi
done
set -- ${ARGS[@]+"${ARGS[@]}"}

xeno_build_apps run-desktop
echo "[run-desktop] Starting xeno-desktop..."
# Give the app the Windows PATH entries back (clip.exe, explorer.exe, wslview) and BROWSER=wslview.
wsl_prepare_app_env
exec "$(xeno_exe xeno-desktop)" "$@"

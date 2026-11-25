#!/usr/bin/env bash
# Start the Xeno client in this distro when you sign in to Windows. WSL has no tray agent, so this starts the client
# worker (xeno connect --foreground) from a hidden launcher in the Windows Startup folder; the launcher runs it
# through wsl.exe and keeps the distro running while the client runs. It uses the xeno tool install.sh put in place,
# so run install.sh first. No admin rights are needed. Running it again rewrites the launcher; remove-from-startup.sh
# undoes it, and remove.sh does too.
# WSL 2 version: registers the client in this distro; use scripts\windows\run-at-startup.bat for the Windows client.
#
# Usage: run-at-startup.sh [--now]
#   --now   also start the client now, the way Windows starts it at sign-in (any running client is stopped first)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-at-startup] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"

NOW=0
for arg in "$@"; do
    case "$arg" in
        --now) NOW=1 ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[run-at-startup] Unknown argument: $arg" >&2
           echo "Usage: run-at-startup.sh [--now]" >&2
           exit 2 ;;
    esac
done

CLI="$HOME/.dotnet/tools/xeno"
[ -x "$CLI" ] || die "The xeno tool is not installed in this distro ($CLI is missing). Run scripts/wsl2/install.sh first."

# A launcher does not read ~/.bashrc, so it passes DOTNET_ROOT when .NET is installed in a user folder. The worker
# never starts the tray agent, and WSL has none, so XENO_AGENT_AUTOSTART=0 keeps it that way.
set -- env XENO_AGENT_AUTOSTART=0
if [ -n "${DOTNET_ROOT:-}" ]; then
    set -- "$@" "DOTNET_ROOT=$DOTNET_ROOT"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
    set -- "$@" "DOTNET_ROOT=$HOME/.dotnet"
fi
wsl_startup_install xeno-client "$@" "$CLI" connect --foreground
echo "[run-at-startup] The Xeno client in this distro ($WSL_DISTRO_NAME) will start when you sign in to Windows."

if [ "$NOW" -eq 1 ]; then
    "$CLI" stop >/dev/null 2>&1 || true
    wsl_startup_run xeno-client
    echo "[run-at-startup] Started the Xeno client. Check it with: xeno status"
fi

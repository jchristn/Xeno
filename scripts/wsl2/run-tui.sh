#!/usr/bin/env sh
# Builds and runs the terminal app (xeno tui) in this terminal. WSL 2 counterpart of scripts/linux/run-tui.sh.
# Under WSL 2 NAT networking, a localhost --server URL is pointed at the Windows host when that is where the server answers.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl2   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"

# A server running on Windows is not at localhost under WSL 2 NAT networking; rewrite --server when needed.
_wsl_argc=$#
_wsl_next=0
for _wsl_arg in "$@"; do
    if [ "$_wsl_next" = 1 ]; then
        _wsl_arg="$(wsl_resolve_local_url "$_wsl_arg")"
        _wsl_next=0
    fi
    [ "$_wsl_arg" = --server ] && _wsl_next=1
    set -- "$@" "$_wsl_arg"
done
shift "$_wsl_argc"
wsl_run "$SCRIPT_DIR/../linux/run-tui.sh" "$@"

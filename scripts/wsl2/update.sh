#!/usr/bin/env sh
# Updates the Xeno installed in this distro and restarts the client worker if it was running. WSL 2 counterpart of scripts/linux/update.sh.
# With WSLg, the "Xeno" and "Xeno Agent" launcher entries also appear in the Windows Start menu. A Windows npm is not used for the dashboard build.
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
# Put ~/.dotnet/tools on the PATH now, so the scripts find xeno right after dotnet tool install.
mkdir -p "$HOME/.dotnet/tools"
case ":$PATH:" in *":$HOME/.dotnet/tools:"*) ;; *) PATH="$HOME/.dotnet/tools:$PATH"; export PATH ;; esac
wsl_run "$SCRIPT_DIR/../linux/update.sh" "$@"

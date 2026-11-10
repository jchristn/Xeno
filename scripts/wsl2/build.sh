#!/usr/bin/env bash
# Build the whole Xeno solution (server, client, desktop app, tray agent, terminal app, and tests).
# WSL 2 version: uses the Linux dotnet inside this distro, never a Windows one.
#
# Usage: build.sh [extra dotnet build arguments...]
#   XENO_CONFIGURATION=Release build.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[build] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"

wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"

echo "[build] Building src/Xeno.sln (${XENO_CONFIGURATION})..."
dotnet build "$XENO_REPO_ROOT/src/Xeno.sln" -c "$XENO_CONFIGURATION" "$@"

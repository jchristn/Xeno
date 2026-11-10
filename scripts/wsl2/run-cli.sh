#!/usr/bin/env bash
# Build and run any xeno command from source.
# WSL 2 version: uses the Linux dotnet and the client home in this distro (~/.xeno), not the Windows one.
#
# Usage: run-cli.sh <xeno arguments...>
#   run-cli.sh status
#   run-cli.sh server show
#   run-cli.sh server use http://localhost:9000
#   run-cli.sh login -u admin@example.com
#   run-cli.sh start | stop | restart | logs -f
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[run-cli] ERROR: $*" >&2; exit 1; }

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

xeno_build_apps run-cli >&2
exec "$(xeno_exe xeno)" "$@"

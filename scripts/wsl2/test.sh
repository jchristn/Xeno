#!/usr/bin/env bash
# Build and run the Touchstone test suites through the console runner (src/Test.Automated).
# WSL 2 version: uses the Linux dotnet inside this distro, never a Windows one.
#
# Usage: test.sh [test runner arguments...]
#   test.sh                       run every suite
#   test.sh --suite Tui           run one suite (SharedClient, Tray, Desktop, Tui, ClientCli, ...)
#   test.sh --results results.json
# Set XENO_TUI_SNAPSHOT_DIR to a folder to save the terminal app's rendered frames while the Tui suite runs.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[test] ERROR: $*" >&2; exit 1; }

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

echo "[test] Building the test runner (${XENO_CONFIGURATION})..."
xeno_quiet_build "$XENO_REPO_ROOT/src/Test.Automated" -c "$XENO_CONFIGURATION"

echo "[test] Running tests..."
exec dotnet run --project "$XENO_REPO_ROOT/src/Test.Automated" -c "$XENO_CONFIGURATION" --no-build -- "$@"

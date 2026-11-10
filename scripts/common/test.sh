#!/usr/bin/env bash
# Build and run the Touchstone test suites through the console runner (src/Test.Automated).
#
# Usage: test.sh [test runner arguments...]
#   test.sh                       run every suite
#   test.sh --suite Tui           run one suite (SharedClient, Tray, Desktop, Tui, ClientCli, ...)
#   test.sh --results results.json
# Set XENO_TUI_SNAPSHOT_DIR to a folder to save the terminal app's rendered frames while the Tui suite runs.
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

echo "[test] Building the test runner (${XENO_CONFIGURATION})..."
xeno_quiet_build "${XENO_REPO_ROOT}/src/Test.Automated" -c "${XENO_CONFIGURATION}"

echo "[test] Running tests..."
exec dotnet run --project "${XENO_REPO_ROOT}/src/Test.Automated" -c "${XENO_CONFIGURATION}" --no-build -- "$@"

#!/usr/bin/env bash
# Build the whole Xeno solution (server, client, desktop app, tray agent, terminal app, and tests).
#
# Usage: build.sh [extra dotnet build arguments...]
#   XENO_CONFIGURATION=Release build.sh
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

echo "[build] Building src/Xeno.sln (${XENO_CONFIGURATION})..."
dotnet build "${XENO_REPO_ROOT}/src/Xeno.sln" -c "${XENO_CONFIGURATION}" "$@"

#!/usr/bin/env bash
# Build the whole Xeno solution (server, client, desktop app, tray agent, terminal app, and tests).
#
# Usage: build.sh [extra dotnet build arguments...]
#   XENO_CONFIGURATION=Release build.sh
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

echo "[build] Building src/Xeno.sln (${XENO_CONFIGURATION})..."
dotnet build "${XENO_REPO_ROOT}/src/Xeno.sln" -c "${XENO_CONFIGURATION}" "$@"

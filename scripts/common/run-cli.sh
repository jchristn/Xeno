#!/usr/bin/env bash
# Build and run any xeno command from source.
#
# Usage: run-cli.sh <xeno arguments...>
#   run-cli.sh status
#   run-cli.sh server show
#   run-cli.sh server use http://localhost:9000
#   run-cli.sh login -u admin@example.com
#   run-cli.sh start | stop | restart | logs -f
set -euo pipefail

# shellcheck source=scripts/common/lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

xeno_build_apps run-cli >&2
exec "$(xeno_exe xeno)" "$@"

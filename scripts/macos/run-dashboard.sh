#!/usr/bin/env bash
# Run the Xeno server web dashboard (Vite dev server) at http://localhost:9100.
# macOS entry point: checks the OS, then runs scripts/common/run-dashboard.sh, which has the usage and the details.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

[ "$(uname -s)" = Darwin ] || die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 (or scripts/wsl) on WSL, or scripts\\windows on Windows."
exec "${SCRIPT_DIR}/../common/run-dashboard.sh" "$@"

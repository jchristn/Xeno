#!/usr/bin/env bash
# Run a local Xeno server and web dashboard, then a client app connected to them.
# macOS entry point: checks the OS, then runs scripts/common/run-local.sh, which has the usage and the details.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

[ "$(uname -s)" = Darwin ] || die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 (or scripts/wsl) on WSL, or scripts\\windows on Windows."
exec "${SCRIPT_DIR}/../common/run-local.sh" "$@"

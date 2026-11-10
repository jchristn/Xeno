#!/usr/bin/env bash
# Remove the Xeno installation (the xeno tool and the apps). Keeps the client data in ~/.xeno.
# macOS entry point: checks the OS, then runs scripts/common/remove.sh, which has the usage and the details.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

[ "$(uname -s)" = Darwin ] || die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 (or scripts/wsl) on WSL, or scripts\\windows on Windows."
exec "${SCRIPT_DIR}/../common/remove.sh" "$@"

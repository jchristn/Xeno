#!/usr/bin/env bash
# Remove the Xeno installation (the xeno tool and the apps). Keeps the client data in ~/.xeno.
# Linux entry point: checks the OS, then runs scripts/common/remove.sh, which has the usage and the details.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

[ "$(uname -s)" = Linux ] || die "This script is for Linux. Use scripts/macos on macOS or scripts\\windows on Windows."
# A Microsoft kernel alone is not WSL (Docker Desktop containers have one too); WSL also has interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1); they handle the Windows PATH, line endings and WSLg."
fi
exec "${SCRIPT_DIR}/../common/remove.sh" "$@"

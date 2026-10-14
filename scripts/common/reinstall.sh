#!/usr/bin/env bash
# Remove and then freshly install Xeno from this checkout (the xeno tool, the desktop app, and the tray agent).
# Your client configuration in ~/.xeno is kept. Arguments are passed to install.sh.
#
# Usage: reinstall.sh [--no-apps] [--no-dashboard]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "[reinstall] Removing the current installation..."
"${SCRIPT_DIR}/remove.sh"

echo
echo "[reinstall] Installing..."
exec "${SCRIPT_DIR}/install.sh" "$@"

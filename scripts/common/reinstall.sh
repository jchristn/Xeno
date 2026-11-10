#!/usr/bin/env bash
# Remove and then freshly install Xeno from this checkout (the xeno tool, the desktop app, and the tray agent).
# Your client configuration in ~/.xeno is kept. Arguments are passed to install.sh.
#
# Usage: reinstall.sh [--no-apps] [--no-dashboard]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

# Check the arguments before removing anything, so a typo does not leave Xeno uninstalled.
INSTALL_APPS=1
for arg in "$@"; do
    case "${arg}" in
        --no-apps) INSTALL_APPS=0 ;;
        --no-dashboard) ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[reinstall] Unknown argument: ${arg}" >&2; exit 2 ;;
    esac
done

# Before removing anything, so a package that cannot be installed leaves the current install in place.
if [ "${INSTALL_APPS}" -eq 1 ]; then
    native_install_deps "Xeno" icu gui clipboard
else
    native_install_deps "Xeno" icu clipboard
fi

echo "[reinstall] Removing the current installation..."
"${SCRIPT_DIR}/remove.sh"

echo
echo "[reinstall] Installing..."
exec "${SCRIPT_DIR}/install.sh" "$@"

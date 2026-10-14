#!/usr/bin/env bash
# Publish and install the Xeno desktop app (xeno-desktop) and tray agent (xeno-tray) for this machine.
#   macOS: builds ~/Applications/Xeno.app with scripts/macos/build-app.sh.
#   Linux: publishes to ~/.local/share/xeno/app (or $XDG_DATA_HOME/xeno/app) and adds "Xeno" and "Xeno Agent"
#          entries to ~/.local/share/applications.
# The apps look in these folders, so xeno tui and the tray agent can open the desktop app.
#
# Usage: install-apps.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

if [ "$(uname -s)" = "Darwin" ]; then
    exec "${SCRIPT_DIR}/../macos/build-app.sh" --install
fi

case "$(uname -m)" in
    aarch64|arm64) RID="linux-arm64" ;;
    *) RID="linux-x64" ;;
esac

TARGET="$(xeno_linux_install_dir)"
echo
echo "[install-apps] Publishing xeno-desktop and xeno-tray (${RID}) to ${TARGET}..."
rm -rf "${TARGET}"
mkdir -p "${TARGET}"
dotnet publish "${XENO_DESKTOP_PROJECT}" -c Release -r "${RID}" --self-contained false --nologo -v quiet -o "${TARGET}"
cp "${XENO_REPO_ROOT}/assets/logo.png" "${TARGET}/xeno.png"

APPLICATIONS="${XDG_DATA_HOME:-${HOME}/.local/share}/applications"
mkdir -p "${APPLICATIONS}"
cat > "${APPLICATIONS}/xeno.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Xeno
Comment=Manage this machine on the Xeno network
Exec=${TARGET}/xeno-desktop
Icon=${TARGET}/xeno.png
Terminal=false
Categories=Network;Utility;
DESKTOP
cat > "${APPLICATIONS}/xeno-agent.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Xeno Agent
Comment=Xeno client status and launchers in the notification area
Exec=${TARGET}/xeno-tray
Icon=${TARGET}/xeno.png
Terminal=false
Categories=Network;Utility;
DESKTOP
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "${APPLICATIONS}" >/dev/null 2>&1 || true

echo "[install-apps] Installed. Start the desktop app from your app launcher (Xeno) or run ${TARGET}/xeno-desktop."

#!/usr/bin/env bash
# Install Xeno from this checkout:
#   1. builds the client's local web dashboard (dashboard/dist-local) so it is embedded in xeno,
#   2. packs src/Xeno.Client and installs it as the global .NET tool "xeno" (which includes "xeno tui"),
#   3. installs the desktop app and the tray agent (stopping the client worker and the apps first):
#        macOS: ~/Applications/Xeno.app (xeno-desktop, xeno-tray, and xeno inside), see scripts/macos/build-app.sh
#        Linux: ~/.local/share/xeno/app plus "Xeno" and "Xeno Agent" launcher entries
# Your client configuration in ~/.xeno is kept. New installs point at the Xeno cloud server.
#
# Usage: install.sh [--no-apps] [--no-dashboard]
#   --no-apps        install only the xeno tool
#   --no-dashboard   skip the local dashboard build (keeps whatever dashboard/dist-local holds)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"

INSTALL_APPS=1
BUILD_DASHBOARD=1
for arg in "$@"; do
    case "${arg}" in
        --no-apps) INSTALL_APPS=0 ;;
        --no-dashboard) BUILD_DASHBOARD=0 ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[install] Unknown argument: ${arg}" >&2; exit 2 ;;
    esac
done

# Install the system packages Xeno needs on Linux when they are missing (INSTALL_SYSTEM_PACKAGES=0 to only report them):
# ICU for .NET, the Avalonia libraries for the desktop app, and a clipboard tool for xeno tui.
if [ "${INSTALL_APPS}" -eq 1 ]; then
    native_install_deps "Xeno" icu gui clipboard
else
    native_install_deps "Xeno" icu clipboard
fi
xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

# A running worker or app keeps using the old files; stop them before replacing the tool and the apps.
CLI="$(xeno_any_cli)"
if [ -n "${CLI}" ]; then
    echo "[install] Stopping the client worker if it is running..."
    "${CLI}" stop >/dev/null 2>&1 || true
fi
pkill -x xeno-desktop 2>/dev/null || true
pkill -x xeno-tray 2>/dev/null || true

if [ "${BUILD_DASHBOARD}" -eq 1 ]; then
    if command -v npm >/dev/null 2>&1; then
        echo
        echo "[install] Building the local web dashboard..."
        (cd "${XENO_REPO_ROOT}/dashboard" && npm install --silent && npm run build:local)
    else
        echo "[install] WARNING: npm not found; the local web dashboard will not be embedded in xeno." >&2
    fi
fi

echo
echo "[install] Packing the xeno tool (Release)..."
mkdir -p "${XENO_TOOL_PACKAGES}"
rm -f "${XENO_TOOL_PACKAGES}"/xeno*.nupkg "${XENO_TOOL_PACKAGES}"/Xeno.Client*.nupkg
dotnet pack "${XENO_CLIENT_PROJECT}" -c Release --nologo -v quiet -o "${XENO_TOOL_PACKAGES}"

echo
echo "[install] Installing the xeno global tool..."
dotnet tool uninstall --global xeno >/dev/null 2>&1 || true
dotnet tool uninstall --global Xeno.Client >/dev/null 2>&1 || true
if xeno_tool_installed; then
    echo "[install] ERROR: could not uninstall the xeno global tool. Close every xeno process (xeno tui, xeno connect) and try again." >&2
    exit 1
fi
dotnet tool install --global xeno --add-source "${XENO_TOOL_PACKAGES}"

if [ "${INSTALL_APPS}" -eq 1 ]; then
    "${SCRIPT_DIR}/install-apps.sh"
fi

echo
echo "[install] Completed."
if ! command -v xeno >/dev/null 2>&1; then
    echo "[install] Add ~/.dotnet/tools to your PATH to run xeno and xeno tui."
fi
echo "[install] Try: xeno status | xeno tui | xeno server show"

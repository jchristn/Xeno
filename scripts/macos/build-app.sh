#!/usr/bin/env bash
# Build Xeno.app, a macOS app bundle holding the desktop app (xeno-desktop, the bundle's main executable), the tray
# agent (xeno-tray, shown as "Xeno Agent"), and the xeno CLI that runs the client worker and the terminal app.
# The bundle is ad-hoc signed, which is fine for running it on this Mac.
#
# Usage: build-app.sh [--install] [--output <dir>] [--self-contained]
#   --install          copy the bundle to ~/Applications/Xeno.app (replacing an older copy)
#   --output <dir>     where to build the bundle (default artifacts/macos)
#   --self-contained   bundle the .NET runtime so the app runs without .NET installed
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/../common/lib.sh"

die() { echo "Error: $*" >&2; exit 1; }

[ "$(uname -s)" = "Darwin" ] || die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 (or scripts/wsl) on WSL, or scripts\\windows on Windows."

INSTALL=0
OUTPUT="${XENO_REPO_ROOT}/artifacts/macos"
SELF_CONTAINED=false
while [ $# -gt 0 ]; do
    case "$1" in
        --install) INSTALL=1; shift ;;
        --output) OUTPUT="${2:?--output requires a directory}"; shift 2 ;;
        --self-contained) SELF_CONTAINED=true; shift ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[build-app] Unknown argument: $1" >&2; exit 2 ;;
    esac
done

xeno_require_cmd dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"

case "$(uname -m)" in
    arm64) RID="osx-arm64" ;;
    *) RID="osx-x64" ;;
esac

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${XENO_DESKTOP_PROJECT}" | head -1)"
VERSION="${VERSION:-0.0.0}"
APP="${OUTPUT}/Xeno.app"

echo "[build-app] Publishing Xeno ${VERSION} (${RID}, self-contained ${SELF_CONTAINED})..."
rm -rf "${APP}"
mkdir -p "${APP}/Contents/MacOS" "${APP}/Contents/Resources"
dotnet publish "${XENO_DESKTOP_PROJECT}" -c Release -r "${RID}" --self-contained "${SELF_CONTAINED}" --nologo -v quiet \
    -o "${APP}/Contents/MacOS"

echo "[build-app] Creating the app icon..."
ICONSET="$(mktemp -d "${TMPDIR:-/tmp}/xeno-icon.XXXXXX")/AppIcon.iconset"
mkdir -p "${ICONSET}"
for size in 16 32 128 256 512; do
    sips -z "${size}" "${size}" "${XENO_REPO_ROOT}/assets/logo.png" --out "${ICONSET}/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "${double}" "${double}" "${XENO_REPO_ROOT}/assets/logo.png" --out "${ICONSET}/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "${ICONSET}" -o "${APP}/Contents/Resources/AppIcon.icns"
rm -rf "$(dirname "${ICONSET}")"

cat > "${APP}/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Xeno</string>
    <key>CFBundleDisplayName</key>
    <string>Xeno</string>
    <key>CFBundleIdentifier</key>
    <string>ai.xenocloud.xeno</string>
    <key>CFBundleExecutable</key>
    <string>xeno-desktop</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.developer-tools</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
PLIST
printf 'APPL????' > "${APP}/Contents/PkgInfo"
plutil -lint "${APP}/Contents/Info.plist" >/dev/null

echo "[build-app] Signing (ad-hoc)..."
codesign --force --deep --sign - "${APP}" >/dev/null 2>&1 || echo "[build-app] WARNING: codesign failed; the app may need to be opened with right-click > Open." >&2

if [ "${INSTALL}" -eq 1 ]; then
    pkill -x xeno-desktop 2>/dev/null || true
    pkill -x xeno-tray 2>/dev/null || true
    mkdir -p "${HOME}/Applications"
    rm -rf "${HOME}/Applications/Xeno.app"
    ditto "${APP}" "${HOME}/Applications/Xeno.app"
    echo "[build-app] Installed ${HOME}/Applications/Xeno.app. Open it from Launchpad or Spotlight (Xeno)."
else
    echo "[build-app] Built ${APP}. Open it with: open \"${APP}\""
fi

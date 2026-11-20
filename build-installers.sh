#!/bin/sh
# Build every Xeno installer this machine's operating system can produce into installers/<version>/:
#   macOS  .pkg and .dmg (osx-x64, osx-arm64), and the portable .tar.gz archives
#   Linux  .deb, .rpm, and AppImage (linux-x64, linux-arm64), and the portable .tar.gz archives
# plus SHA256SUMS for the folder. Run build-installers.bat on Windows for the setup .exe and .zip files.
# All three operating systems build together in CI: push a v<version> tag (see .github/workflows/release.yml).
#
# Usage: ./build-installers.sh [version] [--clean]
#   version   override the version (default: <Version> in src/Directory.Build.props)
#   --clean   rebuild the local web dashboard even if dashboard/dist-local exists
set -eu

cd "$(dirname "$0")"

VERSION_ARGS=""
CLEAN=""
for arg in "$@"; do
    case "$arg" in
        --clean) CLEAN="--clean" ;;
        -h|--help) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        -*) echo "Unknown option: $arg" >&2; exit 2 ;;
        *) VERSION_ARGS="--version $arg" ;;
    esac
done

case "$(uname -s)" in
    Darwin|Linux) ;;
    *) echo "Use build-installers.bat on Windows." >&2; exit 1 ;;
esac

echo "Building the Xeno publisher..."
dotnet build src/Xeno.Publisher/Xeno.Publisher.csproj -c Release --nologo -v q
PUB="src/Xeno.Publisher/bin/Release/net10.0/xeno-publish.dll"

# shellcheck disable=SC2086
VERSION="$(dotnet "$PUB" --print-version $VERSION_ARGS)"
OUT="installers/$VERSION"

echo
echo "Version : $VERSION"
echo "Output  : $OUT"

# shellcheck disable=SC2086
dotnet "$PUB" --channel installers --sums --output "$OUT" $VERSION_ARGS $CLEAN

echo
echo "Installers are in $OUT. Install one, open a new terminal, and run: xeno --version"

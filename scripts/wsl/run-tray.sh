#!/usr/bin/env sh
# Builds and runs the Xeno Agent tray app (xeno-tray). WSL 1 counterpart of scripts/linux/run-tray.sh.
# WSL 2 needs WSLg; WSL 1 cannot run GUI apps.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_die "The tray agent is a GUI app and WSL 1 cannot show it. Use scripts/windows/run-tray.bat from Windows, scripts/wsl/run-tui.sh for the terminal app, or convert this distro to WSL 2 (wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2) for WSLg."

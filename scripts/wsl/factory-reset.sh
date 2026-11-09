#!/usr/bin/env sh
# Resets the Xeno client in this distro (~/.xeno, or XENO_CLIENT_HOME). WSL 1 counterpart of scripts/linux/factory-reset.sh.
# It only touches this distro; the Windows client in %USERPROFILE%\.xeno is separate (scripts/windows/factory-reset.bat).
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
wsl_run "$SCRIPT_DIR/../linux/factory-reset.sh" "$@"

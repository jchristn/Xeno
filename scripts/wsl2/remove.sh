#!/usr/bin/env sh
# Removes the Xeno tool and apps installed in this distro. WSL 2 counterpart of scripts/linux/remove.sh.
# Keeps ~/.xeno. The Windows installation, if any, is separate (scripts/windows/remove.bat).
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl2   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
wsl_run "$SCRIPT_DIR/../linux/remove.sh" "$@"

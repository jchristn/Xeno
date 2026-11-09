#!/usr/bin/env sh
# Runs the server web dashboard (Vite) at http://localhost:9100. WSL 2 counterpart of scripts/linux/run-dashboard.sh.
# Needs Node.js inside WSL (a Windows npm on the PATH is ignored). Windows browsers reach it at localhost through WSL's localhost forwarding.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl2   # "wsl" in scripts/wsl, "wsl2" in scripts/wsl2
# shellcheck source=SCRIPTDIR/_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
wsl_require_cmd npm "Install Node.js 20 or later inside WSL (for example with nvm: https://github.com/nvm-sh/nvm)."
wsl_run "$SCRIPT_DIR/../linux/run-dashboard.sh" "$@"

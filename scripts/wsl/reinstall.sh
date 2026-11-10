#!/usr/bin/env bash
# Remove and then freshly install Xeno in this WSL distro from this checkout (the xeno tool, the desktop app, and
# the tray agent): the same steps as remove.sh, then install.sh. Your client configuration in ~/.xeno is kept.
# WSL 1 version: works on this distro only; use scripts\windows\reinstall.bat for Windows.
#
# Usage: reinstall.sh [--no-apps] [--no-dashboard]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "[reinstall] ERROR: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
# Create ~/.dotnet/tools first so wsl_sanitize_path puts it on PATH for the post-install check.
mkdir -p "$HOME/.dotnet/tools"
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# shellcheck source=../common/lib.sh
. "$REPO_ROOT/scripts/common/lib.sh"


INSTALL_APPS=1
BUILD_DASHBOARD=1
for arg in "$@"; do
    case "$arg" in
        --no-apps) INSTALL_APPS=0 ;;
        --no-dashboard) BUILD_DASHBOARD=0 ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[reinstall] Unknown argument: $arg" >&2; exit 2 ;;
    esac
done

# The desktop app needs a display: WSLg on WSL 2, an X server on Windows on WSL 1.
if [ "$INSTALL_APPS" -eq 1 ] && [ -z "${DISPLAY:-}" ]; then
    echo "[install] WSL 1 has no WSLg and DISPLAY is not set, so only the xeno tool is installed. For the desktop app, start an X server on Windows, set DISPLAY (for example export DISPLAY=localhost:0), and install again. Pass --no-apps to skip this note." >&2
    INSTALL_APPS=0
fi

# Install the system packages Xeno needs in this distro when they are missing (INSTALL_SYSTEM_PACKAGES=0 to only
# report them): ICU for .NET, the Avalonia libraries for the desktop app, a clipboard tool for xeno tui, and wslu and
# xdg-utils so links open in the Windows browser. Before the dotnet check, which needs ICU, and before removing anything.
if [ "$INSTALL_APPS" -eq 1 ]; then
    wsl_install_deps "Xeno" icu gui clipboard browser
else
    wsl_install_deps "Xeno" icu clipboard browser
fi
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_require_cmd pkill "Install procps inside WSL (for example: sudo apt install procps)."

# Stops the worker and the apps, then uninstalls the xeno tool and the apps in this distro.
do_remove() {
    local cli target applications
    cli="$(xeno_any_cli)"
    if [ -n "$cli" ]; then
        echo "[remove] Stopping the client worker if it is running..."
        "$cli" stop >/dev/null 2>&1 || true
    fi

    echo "[remove] Quitting the desktop app and tray agent if they are running..."
    pkill -x xeno-desktop 2>/dev/null || true
    pkill -x xeno-tray 2>/dev/null || true

    echo "[remove] Uninstalling the xeno global tool if present..."
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "[remove] WARNING: dotnet was not found, so the xeno tool uninstall was skipped. Install the .NET SDK inside WSL and run remove again to uninstall it." >&2
    elif xeno_tool_installed; then
        dotnet tool uninstall --global xeno >/dev/null 2>&1 || true
        dotnet tool uninstall --global Xeno.Client >/dev/null 2>&1 || true
        if xeno_tool_installed; then
            echo "[remove] ERROR: could not uninstall the xeno global tool. Close every xeno process (xeno tui, xeno connect) and try again." >&2
            exit 1
        fi
    else
        echo "[remove] The xeno global tool is not installed; nothing to uninstall."
    fi

    target="$(xeno_linux_install_dir)"
    applications="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
    if [ -d "$target" ]; then
        echo "[remove] Removing $target"
        rm -rf "$target"
    fi
    rm -f "$applications/xeno.desktop" "$applications/xeno-agent.desktop"
    echo "[remove] Completed. Client data in $(xeno_client_home) was kept."
}

# Builds the local dashboard, installs the xeno global tool and, with INSTALL_APPS=1, the desktop app.
# Reads INSTALL_APPS and BUILD_DASHBOARD.
do_install() {
    local cli rid target applications exec_line
    # A running worker or app keeps using the old files; stop them before replacing the tool and the apps.
    cli="$(xeno_any_cli)"
    if [ -n "$cli" ]; then
        echo "[install] Stopping the client worker if it is running..."
        "$cli" stop >/dev/null 2>&1 || true
    fi
    pkill -x xeno-desktop 2>/dev/null || true
    pkill -x xeno-tray 2>/dev/null || true

    if [ "$BUILD_DASHBOARD" -eq 1 ]; then
        if command -v npm >/dev/null 2>&1; then
            echo
            echo "[install] Building the local web dashboard..."
            (cd "$XENO_REPO_ROOT/dashboard" && npm install --silent && npm run build:local)
        elif PATH="$WSL_ORIGINAL_PATH" command -v npm >/dev/null 2>&1; then
            echo "[install] WARNING: npm is only installed on the Windows side, so the local web dashboard will not be embedded in xeno. Install Node.js 20 or later inside WSL to include it." >&2
        else
            echo "[install] WARNING: npm not found; the local web dashboard will not be embedded in xeno. Install Node.js 20 or later inside WSL to include it." >&2
        fi
    fi

    echo
    echo "[install] Packing the xeno tool (Release)..."
    mkdir -p "$XENO_TOOL_PACKAGES"
    rm -f "$XENO_TOOL_PACKAGES"/xeno*.nupkg "$XENO_TOOL_PACKAGES"/Xeno.Client*.nupkg
    dotnet pack "$XENO_CLIENT_PROJECT" -c Release --nologo -v quiet -o "$XENO_TOOL_PACKAGES"

    echo
    echo "[install] Installing the xeno global tool..."
    dotnet tool uninstall --global xeno >/dev/null 2>&1 || true
    dotnet tool uninstall --global Xeno.Client >/dev/null 2>&1 || true
    if xeno_tool_installed; then
        echo "[install] ERROR: could not uninstall the xeno global tool. Close every xeno process (xeno tui, xeno connect) and try again." >&2
        exit 1
    fi
    dotnet tool install --global xeno --add-source "$XENO_TOOL_PACKAGES"

    if [ "$INSTALL_APPS" -eq 1 ]; then
        case "$(uname -m)" in
            aarch64|arm64) rid="linux-arm64" ;;
            *) rid="linux-x64" ;;
        esac
        target="$(xeno_linux_install_dir)"
        applications="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
        echo
        echo "[install-apps] Publishing xeno-desktop and xeno-tray ($rid) to $target..."
        rm -rf "$target"
        mkdir -p "$target"
        dotnet publish "$XENO_DESKTOP_PROJECT" -c Release -r "$rid" --self-contained false --nologo -v quiet -o "$target"
        cp "$XENO_REPO_ROOT/assets/logo.png" "$target/xeno.png"
        # Nothing reads launcher entries under WSL 1 (there is no WSLg), so none are written; old ones are removed.
        rm -f "$applications/xeno.desktop" "$applications/xeno-agent.desktop"
        exec_line="env XENO_AGENT_AUTOSTART=0"
        if [ -n "${DOTNET_ROOT:-}" ]; then exec_line="$exec_line DOTNET_ROOT=$DOTNET_ROOT"; fi
        wsl_check_libs warning "The Xeno desktop app" "$WSL_AVALONIA_LIBS"
        echo "[install-apps] Installed. With an X server on Windows and DISPLAY set, run: $exec_line \"$target/xeno-desktop\""
    fi

    echo
    echo "[install] Completed."
    # dotnet tool install -g puts xeno in ~/.dotnet/tools, and a user-local .NET in ~/.dotnet also needs
    # DOTNET_ROOT, or new shells cannot run xeno.
    case ":$WSL_ORIGINAL_PATH:" in
        *":$HOME/.dotnet/tools:"*) ;;
        *)
            echo "[install] $HOME/.dotnet/tools is not on your PATH, so new shells will not find xeno. Add this line to ~/.bashrc (or ~/.profile):"
            # shellcheck disable=SC2016 # printed literally for the user to copy
            echo '  export PATH="$PATH:$HOME/.dotnet/tools"' ;;
    esac
    if [ -z "$WSL_ORIGINAL_DOTNET_ROOT" ] && [ "$(command -v dotnet)" = "$HOME/.dotnet/dotnet" ]; then
        echo "[install] .NET is installed in ~/.dotnet, so xeno also needs DOTNET_ROOT. Add this line to ~/.bashrc (or ~/.profile):"
        # shellcheck disable=SC2016 # printed literally for the user to copy
        echo '  export DOTNET_ROOT="$HOME/.dotnet"'
    fi
    echo "[install] Try: xeno status | xeno tui | xeno server show"
    echo "[install] This installs Xeno in this WSL distro only (client home $(xeno_client_home)); use scripts\\windows\\install.bat for Windows."
}

echo "[reinstall] Removing the current installation..."
do_remove

echo
echo "[reinstall] Installing..."
do_install

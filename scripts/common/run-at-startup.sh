#!/usr/bin/env bash
# Start Xeno Agent (the tray agent) when you sign in. The agent connects the Xeno client when it starts, the same as
# the sign-in item the installers add, so this machine joins the Xeno network without opening an app. It uses the
# apps install.sh put in place, so run install.sh first.
#   macOS: writes the LaunchAgent ~/Library/LaunchAgents/ai.xenocloud.xeno.scripts-agent.plist, which starts
#          ~/Applications/Xeno.app/Contents/MacOS/xeno-tray.
#   Linux: writes the XDG autostart entry ~/.config/autostart/xeno-tray.desktop, which starts
#          ~/.local/share/xeno/app/xeno-tray. It runs with a desktop session; for a headless machine, run the client
#          as a service instead (getting-started/LINUX_CLIENT.md, Start On Boot).
# Running it again rewrites the entry. remove-from-startup.sh undoes it, and remove.sh does too.
#
# Usage: run-at-startup.sh [--now]
#   --now   also start Xeno Agent now, not only at the next sign-in
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common/lib.sh
. "${SCRIPT_DIR}/lib.sh"
die() { echo "[run-at-startup] ERROR: $*" >&2; exit 1; }

NOW=0
for arg in "$@"; do
    case "${arg}" in
        --now) NOW=1 ;;
        -h|--help) xeno_usage "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "[run-at-startup] Unknown argument: ${arg}" >&2
           echo "Usage: run-at-startup.sh [--now]" >&2
           exit 2 ;;
    esac
done

if [ "$(uname -s)" = "Darwin" ]; then
    TRAY="${HOME}/Applications/Xeno.app/Contents/MacOS/xeno-tray"
    [ -x "${TRAY}" ] || die "Xeno Agent is not installed (${TRAY} is missing). Run scripts/macos/install.sh first."
    LABEL="$(xeno_startup_label)"
    PLIST="$(xeno_startup_entry)"
    mkdir -p "$(dirname "${PLIST}")"
    cat > "${PLIST}" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>${LABEL}</string>
  <key>ProgramArguments</key>
  <array>
    <string>${TRAY}</string>
  </array>
  <key>RunAtLoad</key>
  <true/>
  <key>ProcessType</key>
  <string>Interactive</string>
  <key>LimitLoadToSessionType</key>
  <string>Aqua</string>
</dict>
</plist>
EOF
    echo "[run-at-startup] Xeno Agent will start when you sign in: ${PLIST}"
    if [ -f "/Library/LaunchAgents/ai.xenocloud.xeno.agent.plist" ]; then
        echo "[run-at-startup] Note: the Xeno installer's sign-in item (/Library/LaunchAgents/ai.xenocloud.xeno.agent.plist) is also present. Only one agent runs at a time."
    fi
    if [ "${NOW}" -eq 1 ]; then
        DOMAIN="gui/$(id -u)"
        launchctl bootout "${DOMAIN}/${LABEL}" >/dev/null 2>&1 || true
        launchctl bootstrap "${DOMAIN}" "${PLIST}" || die "launchctl could not load ${PLIST}."
        echo "[run-at-startup] Started Xeno Agent."
    fi
else
    TRAY="$(xeno_linux_install_dir)/xeno-tray"
    [ -x "${TRAY}" ] || die "Xeno Agent is not installed (${TRAY} is missing). Run scripts/linux/install.sh first."
    ENTRY="$(xeno_startup_entry)"
    mkdir -p "$(dirname "${ENTRY}")"
    cat > "${ENTRY}" <<EOF
[Desktop Entry]
Type=Application
Name=Xeno Agent
Comment=Xeno client status, start and stop, and launchers
Exec=${TRAY}
Icon=xeno
Terminal=false
X-GNOME-Autostart-enabled=true
EOF
    echo "[run-at-startup] Xeno Agent will start when you sign in to your desktop: ${ENTRY}"
    if [ -z "${DISPLAY:-}" ] && [ -z "${WAYLAND_DISPLAY:-}" ]; then
        echo "[run-at-startup] Note: no desktop session was found. Autostart entries run only when you sign in to a desktop; for a headless machine, see Start On Boot in getting-started/LINUX_CLIENT.md."
    fi
    if [ "${NOW}" -eq 1 ]; then
        if [ -z "${DISPLAY:-}" ] && [ -z "${WAYLAND_DISPLAY:-}" ]; then
            die "--now needs a desktop session (DISPLAY or WAYLAND_DISPLAY); Xeno Agent is a tray app."
        fi
        nohup "${TRAY}" >/dev/null 2>&1 &
        echo "[run-at-startup] Started Xeno Agent."
    fi
fi

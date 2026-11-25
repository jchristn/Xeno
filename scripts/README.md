# Xeno Scripts

These scripts build, run, install, and reset Xeno from a source checkout. To install a released version instead, use
the installers described in [INSTALL.md](../INSTALL.md); to build those installers, run `build-installers.sh` or
`build-installers.bat` at the repository root. Each platform has its own folder:

- `scripts/macos/` and `scripts/linux/`: check the OS, then run the shared bash scripts in `scripts/common/`.
- `scripts/windows/`: `.bat` equivalents.
- `scripts/wsl/` (WSL 1) and `scripts/wsl2/` (WSL 2): complete WSL versions of the same scripts (see [WSL](#wsl)).

Every folder has the same scripts with the same names, arguments, messages, and exit codes. Run them from anywhere;
they find the repository root themselves. On Windows, use `scripts\windows\<name>.bat` instead of
`scripts/macos/<name>.sh`. Each script refuses to run on the wrong OS and names the folder to use instead, and
`-h`/`--help` prints its usage before any other check. Scripts with fixed arguments reject anything else with exit
code 2. `build`, `test`, and `run-cli` pass every argument, `-h` included, on to `dotnet` or `xeno`.

| Script | macOS | Linux | Windows | WSL 1 | WSL 2 |
|---|---|---|---|---|---|
| `build`, `test` | yes | yes | yes | yes | yes |
| `run-cli`, `run-client`, `run-tui`, `run-server`, `run-dashboard`, `run-local` | yes | yes | yes | yes | yes |
| `run-desktop` | yes | yes | yes | needs an X server | needs WSLg |
| `run-tray` | yes | yes | yes | refused (no tray) | refused (no tray) |
| `install`, `reinstall`, `update`, `remove`, `factory-reset` | yes | yes | yes | yes | yes |
| `run-at-startup`, `remove-from-startup` | yes | yes | yes | yes (client worker) | yes (client worker) |

Platform-only helpers, which the scripts above use:

- `scripts/common/lib.sh`: settings and functions for the bash scripts (sourced, never run).
- `scripts/common/native-libs.sh`: the Linux system-package checks and installs (sourced by `lib.sh` on Linux).
- `scripts/macos/build-app.sh [--install]`: builds the `Xeno.app` bundle; `install.sh` uses it on macOS.
- `scripts/windows/env.bat`: settings for the `.bat` files, like `lib.sh`.
- `scripts/windows/build-apps.bat`: builds xeno, xeno-desktop, and xeno-tray for the `run-*.bat` files.
- `scripts/windows/local-stack.ps1`: starts and checks the background server and dashboard for `run-local.bat`.
- `scripts/wsl/_wsl-common.sh` and `scripts/wsl2/_wsl-common.sh`: the WSL helper functions (sourced, never run).

## Start at sign-in

`run-at-startup` makes Xeno start when you sign in, using the apps and tool `install` put in place, so run
`install` first. It registers only for your user account and needs no admin rights. Running it again rewrites the
entry; `remove-from-startup` deletes it, and `remove` does too.

| Platform | What starts | Entry |
|---|---|---|
| macOS | Xeno Agent (`~/Applications/Xeno.app`) | LaunchAgent `~/Library/LaunchAgents/ai.xenocloud.xeno.scripts-agent.plist` |
| Linux | Xeno Agent (`~/.local/share/xeno/app/xeno-tray`) | XDG autostart entry `~/.config/autostart/xeno-tray.desktop` |
| Windows | Xeno Agent (`%LOCALAPPDATA%\Programs\Xeno\xeno-tray.exe`) | Value `Xeno Agent` in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| WSL 1, WSL 2 | The client worker (`xeno connect --foreground`), since WSL has no tray | Hidden launcher `xeno-client-wsl-<distro>.vbs` in the Windows Startup folder |

Xeno Agent connects the client when it starts, the same as the sign-in item the installers add. The installers' items
use other names (`ai.xenocloud.xeno.agent` on macOS, `HKLM` on Windows), so both can exist; only one agent runs at a
time, and `remove-from-startup` leaves the installers' items alone. On Linux the user entry has the package's file
name, so it replaces the package's entry for you instead of starting a second agent. Autostart entries run only with
a desktop session; for a headless Linux machine, run the client as a service instead (Start On Boot in
[getting-started/LINUX_CLIENT.md](../getting-started/LINUX_CLIENT.md)).

## Run from source

Each `run-*` script builds what it needs first, so you don't need to build the solution beforehand.

| Script | What it runs |
|---|---|
| `run-desktop.sh` | The desktop app (`xeno-desktop`). It also starts the tray agent and connects. |
| `run-tui.sh` | The terminal app (`xeno tui`) in the current terminal. |
| `run-tray.sh` | The Xeno Agent tray app (`xeno-tray`). |
| `run-cli.sh <args>` | Any `xeno` command, for example `run-cli.sh status` or `run-cli.sh server show`. |
| `run-client.sh` | The client worker in the foreground (`xeno connect --foreground`). The terminal shows connection changes, warnings, and errors; the full log is in the log file (`xeno logs`). |
| `run-server.sh` | A local Xeno server on port 9000, run from `artifacts/local-server` (described below). |
| `run-dashboard.sh` | The server web dashboard (Vite dev server) at `http://localhost:9100`. |
| `run-local.sh [desktop\|tui\|tray\|client]` | Starts a local server and web dashboard in the background, then runs a client app connected to them. |

`run-desktop.sh` and `run-tui.sh` accept the apps' own options:

- `--server <cloud|url|host[:port]>`: points this client at a server and saves the choice.
- `--home <dir>`: uses a different client home directory.
- `--page <page>`: opens a specific page.

See [Choosing the Xeno server](../archive/DESKTOP_AND_TERMINAL.md#choosing-the-xeno-server).

```bash
scripts/macos/run-desktop.sh                                  # the Xeno cloud (default)
scripts/macos/run-tui.sh --server http://localhost:9000       # a local server
scripts/macos/run-tui.sh --server cloud                       # back to the Xeno cloud
scripts/macos/run-local.sh tui                                # local server + dashboard + terminal app
```

### The local server

`run-server.sh` and `run-local.sh` run `src/Xeno.Server` from `artifacts/local-server/`. That folder is ignored by git,
so a local server never changes the tracked `xeno.json`. It contains:

- `xeno.json`, copied from the repository's `xeno.json` the first time.
- The SQLite database, in `data/`.
- The server logs, in `logs/`.

Delete the folder to start over with an empty server. A new local server has the user `admin@xeno` with the password
`password`.

`run-local.sh` gives the client its own home directory: `XENO_LOCAL_HOME`, by default `~/.xeno-local`. Your real
`~/.xeno`, and its Xeno cloud sign-in, are never touched. When the app exits, or you press Ctrl+C, the server and the
dashboard it started are stopped. `run-local.sh` writes their output to:

- `artifacts/local-server/logs/run-local-server.log`
- `artifacts/local-server/logs/run-local-dashboard.log`

## Install, update, and remove

| Script | What it does |
|---|---|
| `install.sh` | Installs `xeno` as a global .NET tool, then installs the desktop app and the tray agent (details below). An existing install is replaced. |
| `reinstall.sh` | Does what `remove.sh` does, then what `install.sh` does. It checks its arguments first, so a typo removes nothing. |
| `update.sh` | Stops the client worker, reinstalls, and starts the worker again if it was running. |
| `remove.sh` | Stops the worker, quits the apps, and uninstalls the tool and the apps, and removes the entry `run-at-startup.sh` added. Keeps `~/.xeno`. Succeeds when nothing is installed, and fails when the tool cannot be uninstalled (for example while `xeno tui` runs on Windows). Without `dotnet`, it still removes the apps and says the tool uninstall was skipped. |
| `run-at-startup.sh [--now]` | Starts Xeno Agent when you sign in; the agent connects the client, like the installers' sign-in item. Run `install.sh` first. `--now` also starts it now. See [Start at sign-in](#start-at-sign-in). |
| `remove-from-startup.sh` | Undoes `run-at-startup.sh`. A running agent or client keeps running. Succeeds when nothing was registered. |
| `factory-reset.sh [--all] [-y]` | Deletes the client's history, logs, state, and preferences but keeps `client.json`. With `--all`, also deletes `client.json`. |
| `macos/build-app.sh [--install]` | Builds `Xeno.app` in `artifacts/macos`. With `--install`, also copies it to `~/Applications`. |

`install.sh` stops the client worker, the desktop app, and the tray agent, so their files can be replaced. Then it
does three things:

1. Builds the local web dashboard into `xeno`.
2. Installs `xeno` as a global .NET tool. The tool includes the terminal app (`xeno tui`).
3. Installs the desktop app and the tray agent:
   - **macOS:** `~/Applications/Xeno.app`
   - **Linux:** `~/.local/share/xeno/app`, plus launcher entries named "Xeno" and "Xeno Agent"
   - **Windows:** `%LOCALAPPDATA%\Programs\Xeno`, plus Start menu shortcuts
   - **WSL:** `~/.local/share/xeno/app` in the distro; see [WSL](#wsl)

`install.sh` accepts `--no-apps` to install only the tool and `--no-dashboard` to skip the dashboard build.
`factory-reset.sh --all` signs the machine out and points it back at the Xeno cloud.

A fresh install points at the Xeno cloud. An existing `client.json` is kept, along with the server it names.

## Build and test

| Script | What it does |
|---|---|
| `build.sh` | Builds `src/Xeno.sln`. |
| `test.sh [--suite <name>]` | Runs the Touchstone suites through `src/Test.Automated`. |

## Environment variables

| Variable | Default | Used by |
|---|---|---|
| `XENO_CONFIGURATION` | `Debug` (`install` always uses `Release`) | `run-*`, `build`, `test` |
| `XENO_CLIENT_HOME` | `~/.xeno` | Every client app and script |
| `XENO_LOCAL_SERVER` | `http://localhost:9000` | `run-local` |
| `XENO_LOCAL_HOME` | `~/.xeno-local` | `run-local` |
| `XENO_SKIP_DASHBOARD` | unset | `run-local` (`1` skips the web dashboard) |
| `XENO_AGENT_AUTOSTART` | unset | The desktop app, the terminal app, and `xeno connect`/`start`/`restart` (including `run-client`), which start the tray agent when there is a desktop session. `0` never starts it, `1` always does |
| `XENO_AUTOCONNECT` | unset | The desktop app, the terminal app, and the tray agent, which connect this computer when they start if you are signed in. `0` turns that off |

## WSL

Inside a WSL distro, use `scripts/wsl2/` (WSL 2) or `scripts/wsl/` (WSL 1), not `scripts/linux/`. The Linux scripts
refuse to run under WSL, and each WSL folder refuses the other WSL version. The WSL scripts work on this distro only:
they use `~/.xeno` inside the distro, and a Windows install of Xeno is separate (use `scripts\windows` for it).

What the WSL scripts do differently:

- **Windows PATH.** Windows entries are dropped from `PATH`, so a Windows `dotnet`, `npm`, or `node` is never used by
  mistake. When a tool exists only on the Windows side, the script says so. Install the .NET 10 SDK (and Node.js 20 or
  later, for the dashboards) inside the distro. `~/.dotnet/tools` is added to `PATH`, and `~/.dotnet` when there is
  no other Linux `dotnet`. Set `WSL_KEEP_WINDOWS_PATH=1` to keep the Windows entries.
- **Line endings and DrvFs.** Scripts checked out with Windows (CRLF) line endings are reported with the fix. A
  checkout on a Windows drive (`/mnt/c/...`) gets a warning: clone into the Linux file system (for example `~/src`)
  for speed and to avoid Windows file locks.
- **Desktop app.** The desktop app uses X11, so `DISPLAY` must be set. WSL 2 gets it from WSLg (`wsl --update` from
  Windows); `WAYLAND_DISPLAY` alone is not enough. WSL 1 has no WSLg and needs an X server on Windows
  with `DISPLAY` set (for example `export DISPLAY=localhost:0`). Without a display, `run-desktop.sh` and
  `run-local.sh [desktop]` stop with a message, and `install.sh`, `reinstall.sh`, and `update.sh` install only the
  tool.
- **Tray agent.** Neither WSLg nor an X server on Windows has a notification area, so the tray icon cannot appear:
  `run-tray.sh` and `run-local.sh tray` stop with a message (set `XENO_WSL_FORCE_TRAY=1` to start it anyway, for
  example under your own X desktop). No app or command starts the tray agent under WSL unless
  `XENO_AGENT_AUTOSTART=1`.
- **Launcher entries.** On WSL 2, `install.sh` writes a "Xeno" launcher entry only (no "Xeno Agent"); it sets
  `XENO_AGENT_AUTOSTART=0` and, for a user-local .NET, `DOTNET_ROOT`. On WSL 1, nothing reads launcher entries, so
  none are written. `remove.sh` deletes entries left by an earlier `scripts/linux` install.
- **Networking.** The server (port 9000) and the web dashboard (port 9100) listen on all addresses, so Windows opens
  them at `http://localhost:9000` and `http://localhost:9100`: WSL 1 shares localhost with Windows, and WSL 2 forwards
  localhost. Under WSL 2 NAT networking, localhost inside the distro is not Windows: when `run-tui.sh`,
  `run-desktop.sh`, or `run-client.sh` get `--server http://localhost:...` and nothing answers inside the distro but
  a server on Windows does, they use the Windows host address and say so. That address can change when Windows
  restarts; `networkingMode=mirrored` in `.wslconfig` avoids the problem. `WSL_NO_HOST_REWRITE=1` turns this off.

## System packages on Linux and WSL

The desktop app and the tray agent (Avalonia) load X11 client libraries at startup, and every .NET program needs
ICU. Desktop distros ship them, but minimal and server images and the stock WSL images often don't (WSLg provides
only the display). Building never loads them, so without them the build succeeds and the app then fails with:

```
System.DllNotFoundException: Unable to load shared library 'libICE.so.6'
Couldn't find a valid ICU package installed on the system
```

- `install.sh`, `reinstall.sh`, and `update.sh` (Linux, WSL 1, WSL 2) install the missing packages themselves, with
  `sudo apt-get install` or `sudo dnf install`, before they change anything: ICU, the Avalonia libraries (unless
  `--no-apps`), and `xclip` when no clipboard tool (`wl-copy`, `xclip`, `xsel`) exists, for copying in `xeno tui`.
  On WSL they also install `wslu` and `xdg-utils`, so links open in the Windows browser. `sudo` may ask for your
  password. Set `INSTALL_SYSTEM_PACKAGES=0` to get the list instead; the script then stops when a required package
  is missing.
- The `run-*` scripts only check: `run-desktop`, `run-tray`, and `run-local desktop|tray` stop before building when a
  library is missing, `run-tui` warns, and every script that builds stops when ICU is missing. They print the
  command to run. The checks use `ldconfig` and do nothing without it.
- The desktop app and the tray agent also print the command and exit with code 1, instead of a stack trace, when a
  library is missing at startup on Linux.

To install them yourself on Debian or Ubuntu (use the `libicu` package your release has, for example `libicu74`):

```bash
sudo apt install libx11-6 libice6 libsm6 libxext6 libxrandr2 libxi6 libxcursor1 libxfixes3 libfontconfig1 libicu74 xclip
sudo apt install wslu xdg-utils    # WSL only
```

On Fedora: `sudo dnf install libX11 libICE libSM libXext libXrandr libXi libXcursor libXfixes fontconfig libicu xclip`.

## Tests

`python3 scripts/tests/test-scripts.py` runs every macOS, Linux, WSL 1, and WSL 2 script against fake `dotnet`,
`uname`, `pgrep`, `pkill`, `npm`, `curl`, and Windows interop tools in a temporary copy of the repository. It checks
the same-set rule, the OS guards, argument handling, the commands each script runs, the install, reinstall, update,
and remove rules, and the files they write. It checks the Windows `.bat` files statically (CRLF endings, `setlocal`,
`endlocal & exit /b`, `-h`), because they cannot run outside Windows. It also runs `bash -n` and, when installed,
`shellcheck` (set `SHELLCHECK` to its path) and the PowerShell parser.

The repository-root `build-*.sh`/`.bat` scripts build and push the Docker images; they are separate from these.

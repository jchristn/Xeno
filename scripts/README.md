# Xeno Scripts

These scripts build, run, install, and reset Xeno from a source checkout. Each platform has its own folder:

- `scripts/macos/` and `scripts/linux/`: thin wrappers around the shared bash scripts in `scripts/common/`.
- `scripts/windows/`: `.bat` equivalents.
- `scripts/wsl/` (WSL 1) and `scripts/wsl2/` (WSL 2): the Linux scripts, adjusted for WSL (see below).

Run them from anywhere; they find the repository root themselves. On Windows, use `scripts\windows\<name>.bat`
instead of `scripts/macos/<name>.sh`.

## Run from source

Each `run-*` script builds what it needs first, so you don't need to build the solution beforehand.

| Script | What it runs |
|---|---|
| `run-desktop.sh` | The desktop app (`xeno-desktop`). It also starts the tray agent. |
| `run-tui.sh` | The terminal app (`xeno tui`) in the current terminal. |
| `run-tray.sh` | The Xeno Agent tray app (`xeno-tray`). |
| `run-cli.sh <args>` | Any `xeno` command, for example `run-cli.sh status` or `run-cli.sh server show`. |
| `run-client.sh` | The client worker in the foreground (`xeno connect --foreground`), with its log in the terminal. |
| `run-server.sh` | A local Xeno server on port 9000, run from `artifacts/local-server` (described below). |
| `run-dashboard.sh` | The server web dashboard (Vite dev server) at `http://localhost:9100`. |
| `run-local.sh [desktop\|tui\|tray\|client]` | Starts a local server and web dashboard in the background, then runs a client app connected to them. |

`run-desktop.sh` and `run-tui.sh` accept the apps' own options:

- `--server <cloud|url|host[:port]>`: points this client at a server and saves the choice.
- `--home <dir>`: uses a different client home directory.
- `--page <page>`: opens a specific page.

See [Choosing the Xeno server](../DESKTOP_AND_TERMINAL.md#choosing-the-xeno-server).

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
| `install.sh` | Installs `xeno` as a global .NET tool, then installs the desktop app and the tray agent (details below). |
| `reinstall.sh` | Runs `remove.sh`, then `install.sh`. |
| `update.sh` | Stops the client worker, reinstalls, and starts the worker again if it was running. |
| `remove.sh` | Stops the worker, quits the apps, and uninstalls the tool and the apps. Keeps `~/.xeno`. |
| `factory-reset.sh [--all] [-y]` | Deletes the client's history, logs, state, and preferences but keeps `client.json`. With `--all`, also deletes `client.json`. |
| `macos/build-app.sh [--install]` | Builds `Xeno.app` in `artifacts/macos`. With `--install`, also copies it to `~/Applications`. |

`install.sh` does three things:

1. Builds the local web dashboard into `xeno`.
2. Installs `xeno` as a global .NET tool. The tool includes the terminal app (`xeno tui`).
3. Installs the desktop app and the tray agent:
   - **macOS:** `~/Applications/Xeno.app`
   - **Linux:** `~/.local/share/xeno/app`, plus launcher entries named "Xeno" and "Xeno Agent"
   - **Windows:** `%LOCALAPPDATA%\Programs\Xeno`, plus Start menu shortcuts

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
| `XENO_AGENT_AUTOSTART` | unset | The desktop app (`0` stops it from starting the tray agent) |

## WSL

Inside a WSL distro, use `scripts/wsl2/` (WSL 2) or `scripts/wsl/` (WSL 1) instead of `scripts/linux/`. They run the
Linux scripts after fixing what usually breaks them under WSL:

- Windows entries are dropped from `PATH`, so a Windows `dotnet`, `npm`, or `node` is never used by mistake. Install
  the .NET 10 SDK (and Node.js, for the dashboards) inside the distro. `~/.dotnet` and `~/.dotnet/tools` are added.
- Scripts checked out with Windows (CRLF) line endings are reported with the fix. Clone into the Linux file system
  (for example `~/src`), not `/mnt/c`, for speed and to avoid Windows file locks.
- The desktop app and the tray agent need WSLg on WSL 2 (`wsl --update`), and WSLg has no notification area for the
  tray icon. WSL 1 cannot run them: `install.sh`, `reinstall.sh`, and `update.sh` add `--no-apps`, and `run-desktop.sh`,
  `run-tray.sh`, and `run-local.sh desktop|tray` stop with a message (use `run-local.sh tui`).
- On WSL 2, a `--server http://localhost:...` URL is pointed at the Windows host when the server runs on Windows.

The WSL installation is separate from a Windows one: it uses `~/.xeno` inside the distro.

The repository-root `build-*.sh`/`.bat` scripts build and push the Docker images; they are separate from these.

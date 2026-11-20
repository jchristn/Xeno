# Xeno Client on macOS

This guide installs and runs the Xeno client on macOS.

## Defaults

- Client settings: `~/.xeno/client.json`
- Client state: `~/.xeno/state.json`
- Local dashboard: `http://127.0.0.1:9343`
- Optional local inference listener: `http://127.0.0.1:11435` when `LocalInferenceServer.Enabled` is true (Ollama keeps its own port, 11434)
- Default cloud server: `https://app.xenocloud.ai:9000` (HTTPS only; plain `http://` on port 9000 is rejected)
- Default cloud WebSocket URL: `wss://app.xenocloud.ai:9000/ws/v1/clients`
- Local Docker server WebSocket URL: `ws://localhost:9000/ws/v1/clients`
- Local Docker server REST URL: `http://localhost:9000`
- Dashboard login for seeded Docker server: `admin@xeno` / `password`
- Default seeded client access key: `default`
- Default REST bearer token for Postman and SDK examples: `default`

## Prerequisites

1. Install the .NET SDK for the repository target framework.
2. Install Node.js and npm.
3. Install Git.
4. Install a provider runtime if you want local models. Ollama is the easiest starting point.
5. Clone the repository.

```bash
git clone https://github.com/jchristn/Xeno.git
cd Xeno
```

## Build and Configure

Do this once before choosing how the client should start.

1. Build the solution.

```bash
dotnet build src/Xeno.sln
```

2. Build the local dashboard assets.

```bash
cd dashboard
npm install
npm run build:local
cd ..
```

3. Set a shell variable for the client command.

```bash
XENO="dotnet $(pwd)/src/Xeno.Client/bin/Debug/net10.0/xeno.dll"
```

4. Initialize the client settings file.

```bash
$XENO init
```

5. New clients connect to the Xeno cloud. If you are connecting to your own server instead, such as a local Docker server, point the client at it. `$XENO server show` prints the server in use, and `$XENO server use cloud` switches back.

```bash
$XENO server use http://localhost:9000
```

6. Log in to the server and store a client access key.

```bash
$XENO login -u admin@xeno --server http://localhost:9000
```

For the seeded Docker server, use password `password`.

7. Add a local Ollama provider.

```bash
$XENO provider add --name local-ollama --type Ollama --endpoint http://127.0.0.1:11434 --allow-model-management
```

8. Advertise a completion model.

```bash
$XENO model add --provider local-ollama --name llama3.2:latest --type completion
```

9. Review the client configuration.

```bash
$XENO config show
$XENO inventory
```

10. Optionally enable the localhost OpenAI/Ollama-compatible inference listener.

Edit `~/.xeno/client.json` and add:

```json
"LocalInferenceServer": {
  "Enabled": true,
  "Host": "127.0.0.1",
  "Port": 11435,
  "EnableCors": true,
  "RequestTimeoutMs": 300000,
  "RoutingMode": "PreferLocal"
}
```

The listener defaults to port 11435 so a local Ollama can keep 11434. A client upgraded from 0.1.x with `"Port": 11434` is moved to 11435 once, and `xeno status` reports the change. To keep a provider off the Xeno network but still use it from this machine (for example your own Ollama Cloud account), add it with `--local-only`.

The listener is loopback-only and uses the client identity from `client.json`. If the WebSocket tunnel is unavailable, local API calls return provider-shaped `502` responses. If a local inference request exceeds `LocalInferenceServer.RequestTimeoutMs`, the listener cancels the tunneled request and returns a provider-shaped `504` response.

## Start Interactively

Run the client yourself from a terminal. This is the simplest way and is best for first-run validation, configuration changes, and development.

1. Start the client in the background. It runs until you stop it or the session ends.

```bash
$XENO start
```

Or run it attached to the terminal (press `Ctrl+C` to stop):

```bash
$XENO start --foreground --worker
```

2. Check status and open the local dashboard.

```bash
$XENO status
$XENO dashboard
$XENO dashboard open
```

3. Watch logs.

```bash
$XENO logs --tail 100
$XENO logs -f
```

4. Restart after configuration or binary changes, or stop the client.

```bash
$XENO restart
$XENO stop
```

To have the client start automatically, use **Start On Login** (when you sign in) or **Start On Boot** (headless, no sign-in).

### Use the desktop, terminal, and tray apps

Instead of the commands above, you can manage the client from an app. All of them share `~/.xeno` with the CLI.

```bash
# Desktop app (it also starts the Xeno Agent in the menu bar)
scripts/macos/run-desktop.sh

# Terminal app
scripts/macos/run-tui.sh          # or: $XENO tui

# Xeno Agent menu bar app on its own
scripts/macos/run-tray.sh

# Install Xeno.app in ~/Applications, plus the xeno tool
scripts/macos/install.sh
```

The apps use the server in `~/.xeno/client.json`, which is the Xeno cloud unless you changed it. To point them at
another server, pass `--server`, for example `scripts/macos/run-desktop.sh --server http://localhost:9000`. You can
also change it on the **General** page. To try everything against a throwaway local server without touching
`~/.xeno`, run `scripts/macos/run-local.sh` (desktop app) or `scripts/macos/run-local.sh tui`.

On macOS the desktop app appears as **Xeno** in the menu bar, with an application menu (About Xeno, Settings), plus
File, Edit, View, Client, Window, and Help menus. The Xeno Agent's menu bar icon opens the desktop app, opens the
terminal app in a new Terminal window, opens the web dashboard, and starts or stops the client. See
[DESKTOP_AND_TERMINAL.md](../archive/DESKTOP_AND_TERMINAL.md) for pages and keyboard shortcuts.

## Start On Login

Start the client automatically when you log in, using a launchd LaunchAgent. It runs in your GUI login session, with access to your home directory and GPU, and stops when you log out.

1. Stop any client you started interactively, so that two workers do not register under the same client ID.

```bash
$XENO stop
```

2. Publish the client to a stable path.

```bash
dotnet publish src/Xeno.Client/Xeno.Client.csproj -c Release -o "$HOME/.xeno/bin"
```

The local dashboard is embedded into the binary from `dashboard/dist-local` at build time. If that directory is missing, the publish still succeeds but the dashboard serves `404`, so run `npm run build:local` first if you have not already.

3. Create the LaunchAgents directory.

```bash
mkdir -p "$HOME/Library/LaunchAgents"
```

4. Create `~/Library/LaunchAgents/ai.xenocloud.client.plist`. Replace `/Users/YOUR_USER` with your actual home directory.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>ai.xenocloud.client</string>
  <key>ProgramArguments</key>
  <array>
    <string>/Users/YOUR_USER/.xeno/bin/xeno</string>
    <string>start</string>
    <string>--foreground</string>
    <string>--worker</string>
  </array>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <true/>
  <key>StandardOutPath</key>
  <string>/Users/YOUR_USER/.xeno/logs/launchd.out.log</string>
  <key>StandardErrorPath</key>
  <string>/Users/YOUR_USER/.xeno/logs/launchd.err.log</string>
</dict>
</plist>
```

`KeepAlive` relaunches the client whenever it exits, including when you exit it yourself with `$XENO stop`. Manage a loaded LaunchAgent with `launchctl`, not with `$XENO stop` and `$XENO restart` — see **Manage the Client**.

5. Load and start the LaunchAgent.

```bash
launchctl load "$HOME/Library/LaunchAgents/ai.xenocloud.client.plist"
launchctl start ai.xenocloud.client
```

6. Monitor the client.

```bash
$HOME/.xeno/bin/xeno status
$HOME/.xeno/bin/xeno ping
$HOME/.xeno/bin/xeno logs --tail 100
tail -f "$HOME/.xeno/logs/launchd.out.log"
```

7. Restart the loaded LaunchAgent after configuration or binary changes.

```bash
launchctl kickstart -k "gui/$(id -u)/ai.xenocloud.client"
```

8. Stop or remove it if needed.

```bash
launchctl stop ai.xenocloud.client
launchctl unload "$HOME/Library/LaunchAgents/ai.xenocloud.client.plist"
```

The LaunchAgent runs in your login session and stops at logout. To keep it running with no one logged in, use **Start On Boot**.

## Start On Boot

Start the client at machine boot with no interactive login, using a launchd LaunchDaemon — the right choice for a dedicated, headless GPU host.

1. Stop any client you started interactively, so that two workers do not register under the same client ID.

```bash
$XENO stop
```

2. Publish the client as your normal user, then copy it to a system path.

```bash
dotnet publish src/Xeno.Client/Xeno.Client.csproj -c Release -o /tmp/xeno-publish
sudo mkdir -p /opt/xeno/bin
sudo cp -a /tmp/xeno-publish/. /opt/xeno/bin/
```

Do not run `dotnet publish` under `sudo` from the repository. It writes root-owned `obj/` and `bin/` directories into your working tree, and your next unprivileged build fails. As with **Start On Login**, run `npm run build:local` first if you have not already, or the published binary serves no local dashboard.

3. Create `/Library/LaunchDaemons/ai.xenocloud.client.plist` (owned by root). Set `UserName` to the account whose `~/.xeno/client.json` holds your configuration, and set `HOME` so the client finds that file. Replace `YOUR_USER` with that account.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>ai.xenocloud.client</string>
  <key>ProgramArguments</key>
  <array>
    <string>/opt/xeno/bin/xeno</string>
    <string>start</string>
    <string>--foreground</string>
    <string>--worker</string>
  </array>
  <key>UserName</key>
  <string>YOUR_USER</string>
  <key>EnvironmentVariables</key>
  <dict>
    <key>HOME</key>
    <string>/Users/YOUR_USER</string>
  </dict>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <true/>
  <key>StandardOutPath</key>
  <string>/Users/YOUR_USER/.xeno/logs/launchd.out.log</string>
  <key>StandardErrorPath</key>
  <string>/Users/YOUR_USER/.xeno/logs/launchd.err.log</string>
</dict>
</plist>
```

4. Set ownership and load it.

```bash
sudo chown root:wheel /Library/LaunchDaemons/ai.xenocloud.client.plist
sudo launchctl load /Library/LaunchDaemons/ai.xenocloud.client.plist
```

5. Monitor it.

```bash
/opt/xeno/bin/xeno status
tail -f /Users/YOUR_USER/.xeno/logs/launchd.out.log
```

6. Restart it after configuration or binary changes.

```bash
sudo launchctl kickstart -k system/ai.xenocloud.client
```

7. Stop or remove it if needed.

```bash
sudo launchctl bootout system /Library/LaunchDaemons/ai.xenocloud.client.plist
```

To pick up new client code later, repeat step 2 and then `sudo launchctl kickstart -k system/ai.xenocloud.client`.

GPU note: a LaunchDaemon starts before login and runs in a non-interactive context. Confirm your GPU/provider runtime (for example Ollama) is reachable from that context — set `UserName` to an account with access and verify with `tail -f /Users/YOUR_USER/.xeno/logs/launchd.out.log` after a reboot.

## Manage the Client

Use these commands during normal operations:

```bash
$XENO status
$XENO config show
$XENO provider list
$XENO provider update --name local-ollama --endpoint http://127.0.0.1:11434 --timeout 300000
$XENO model list
$XENO model update --provider local-ollama --name llama3.2:latest --type completion
$XENO reload
$XENO dashboard
$XENO dashboard open
$XENO logs --tail 100
$XENO stop
$XENO reset
```

If you loaded a LaunchAgent or LaunchDaemon from **Start On Login** or **Start On Boot**, use `launchctl` for the client lifecycle instead of `$XENO stop` and `$XENO restart`. Both plists set `KeepAlive`, so `$XENO stop` asks the worker to exit and launchd immediately relaunches it, and the replacement clears the stop request on startup. `$XENO restart` stops the worker and also starts its own detached client, which can leave two workers running under one client ID. Use `launchctl kickstart -k "gui/$(id -u)/ai.xenocloud.client"` for the agent, or `sudo launchctl kickstart -k system/ai.xenocloud.client` for the daemon, and `launchctl unload`/`bootout` to stop it for good. Note that launchd stops the client with `SIGTERM`, which the client does not handle, so `~/.xeno/state.json` can describe a connection that no longer exists until the client next starts. The remaining commands are safe under launchd: the read-only ones above, and `$XENO reload`, which flags the running worker to re-advertise its inventory without touching the process.

Use the local dashboard to create, update, and delete provider endpoints and advertised models, inspect local request history, review host/provider telemetry, read logs, and manage local settings. Provider/model changes made through the dashboard or CLI are saved to `~/.xeno/client.json` and request an inventory update when the client is connected. If you edit `client.json` directly, run `$XENO reload` or use the Settings page reload action so the connected server receives the updated provider/model inventory.

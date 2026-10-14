# Xeno Client on Linux

This guide installs and runs the Xeno client on Linux.

## Defaults

- Client settings: `~/.xeno/client.json`
- Client state: `~/.xeno/state.json`
- Local dashboard: `http://127.0.0.1:9343`
- Optional local inference listener: `http://127.0.0.1:11434` when `LocalInferenceServer.Enabled` is true
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
  "Port": 11434,
  "EnableCors": true,
  "RequestTimeoutMs": 300000
}
```

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
# Desktop app (it also starts the Xeno Agent in the notification area)
scripts/linux/run-desktop.sh

# Terminal app, which also works over SSH
scripts/linux/run-tui.sh          # or: $XENO tui

# Xeno Agent tray app on its own
scripts/linux/run-tray.sh

# Install the xeno tool, plus the apps in ~/.local/share/xeno/app with launcher entries
scripts/linux/install.sh
```

The apps use the server in `~/.xeno/client.json`, which is the Xeno cloud unless you changed it. To point them at
another server, pass `--server`, for example `scripts/linux/run-tui.sh --server http://10.0.0.20:9000`. You can also
change it on the **General** page. To try everything against a throwaway local server without touching
`~/.xeno`, run `scripts/linux/run-local.sh tui`.

The tray icon needs a desktop environment with a notification area (StatusNotifierItem). Its menu opens the desktop
app, opens the terminal app in your terminal emulator, opens the web dashboard, and starts or stops the client. See
[DESKTOP_AND_TERMINAL.md](../DESKTOP_AND_TERMINAL.md) for pages and keyboard shortcuts.

## Start On Login

Start the client automatically when your user session starts, using a systemd user service. It runs as your user, with access to your home directory and GPU, and stops when your last session ends.

1. Stop any client you started interactively, so that two workers do not register under the same client ID.

```bash
$XENO stop
```

2. Publish the client to a stable path.

```bash
dotnet publish src/Xeno.Client/Xeno.Client.csproj -c Release -o "$HOME/.xeno/bin"
```

The local dashboard is embedded into the binary from `dashboard/dist-local` at build time. If that directory is missing, the publish still succeeds but the dashboard serves `404`, so run `npm run build:local` first if you have not already.

3. Create the systemd user directory.

```bash
mkdir -p "$HOME/.config/systemd/user"
```

4. Create `~/.config/systemd/user/xeno-client.service`.

```ini
[Unit]
Description=Xeno Client
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=%h/.xeno/bin/xeno start --foreground --worker
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30

[Install]
WantedBy=default.target
```

`KillSignal=SIGINT` lets the client run its shutdown path. The client handles `SIGINT`; on the default `SIGTERM` it exits without clearing its state, which leaves `~/.xeno/state.json` reporting a connection that no longer exists.

5. Enable and start the service.

```bash
systemctl --user daemon-reload
systemctl --user enable --now xeno-client.service
```

6. Monitor the service.

```bash
systemctl --user status xeno-client.service
journalctl --user -u xeno-client.service -f
```

7. Restart after configuration or binary changes, or disable it.

```bash
systemctl --user restart xeno-client.service
systemctl --user disable --now xeno-client.service
```

The service starts when you log in and stops when your last session ends. To keep it running with no one logged in, use **Start On Boot**. Once a service is installed, manage the client with `systemctl` rather than `$XENO stop` and `$XENO restart` — see **Manage the Client**.

## Start On Boot

Start the client at machine boot with no interactive login — the right choice for a dedicated, headless GPU host.

1. Stop any client you started interactively, so that two workers do not register under the same client ID.

```bash
$XENO stop
```

2. Publish the client as your normal user, then copy it to a system path.

```bash
dotnet publish src/Xeno.Client/Xeno.Client.csproj -c Release -o /tmp/xeno-publish
sudo install -d -m 755 /opt/xeno/bin
sudo cp -a /tmp/xeno-publish/. /opt/xeno/bin/
```

Do not run `dotnet publish` under `sudo` from the repository. It writes root-owned `obj/` and `bin/` directories into your working tree, and your next unprivileged build fails. As with **Start On Login**, run `npm run build:local` first if you have not already, or the published binary serves no local dashboard.

3. Create `/etc/systemd/system/xeno-client.service`. Run it as the account whose `~/.xeno/client.json` holds your configured providers and server credentials, and set `HOME` so the client finds that file. Replace `YOUR_USER` with that account.

```ini
[Unit]
Description=Xeno Client
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=YOUR_USER
Environment=HOME=/home/YOUR_USER
ExecStart=/opt/xeno/bin/xeno start --foreground --worker
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
```

If your provider runtime runs as a systemd service on the same host, order the client after it so the client does not advertise models before the runtime accepts requests — for example `After=network-online.target ollama.service`.

4. Enable and start it at boot.

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now xeno-client.service
```

5. Monitor it.

```bash
systemctl status xeno-client.service
journalctl -u xeno-client.service -f
```

6. Restart after configuration or binary changes, or disable it.

```bash
sudo systemctl restart xeno-client.service
sudo systemctl disable --now xeno-client.service
```

To pick up new client code later, repeat step 2 and then `sudo systemctl restart xeno-client.service`.

Alternative: keep the per-user service from **Start On Login** and let it run without a login by enabling lingering, which starts your user services at boot: `loginctl enable-linger YOUR_USER`.

GPU note: a service that starts before login runs in a non-interactive session. Confirm your GPU runtime (for example the NVIDIA driver and Ollama) is reachable from that context — run the service as an account with GPU access and verify with `journalctl -u xeno-client.service -f` after a reboot.

## Manage the Client

Use these commands during normal operations:

```bash
$XENO status
$XENO ping
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

If you installed a service from **Start On Login** or **Start On Boot**, use `systemctl` for the client lifecycle instead of `$XENO stop` and `$XENO restart`. `$XENO stop` asks the running worker to exit; systemd then restarts it after `RestartSec`, and the replacement clears the stop request on startup, so the client comes back a few seconds later. `$XENO restart` stops the worker and also starts its own detached client, which can leave two workers running under one client ID. Use `systemctl --user restart xeno-client.service` for a login service, or `sudo systemctl restart xeno-client.service` for a boot service. The remaining commands are safe under a service: the read-only ones above, and `$XENO reload`, which flags the running worker to re-advertise its inventory without touching the process.

Use the local dashboard to create, update, and delete provider endpoints and advertised models, inspect local request history, review host/provider telemetry, read logs, and manage local settings. Provider/model changes made through the dashboard or CLI are saved to `~/.xeno/client.json` and request an inventory update when the client is connected. If you edit `client.json` directly, run `$XENO reload` or use the Settings page reload action so the connected server receives the updated provider/model inventory.

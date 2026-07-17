# Xeno Client on Linux

This guide installs and runs the Xeno client on Linux.

## Defaults

- Client settings: `~/.xeno/client.json`
- Client state: `~/.xeno/state.json`
- Local dashboard: `http://127.0.0.1:9343`
- Optional local inference listener: `http://127.0.0.1:11434` when `LocalInferenceServer.Enabled` is true
- Default cloud WebSocket URL: `ws://app.xenocloud.ai:9000/ws/v1/clients`
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

## Easy Path: Build, Configure, Start, Monitor

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

5. If you are connecting to a local Docker server, set the server URLs.

```bash
$XENO server set-url ws://localhost:9000/ws/v1/clients
$XENO server set-rest-url http://localhost:9000
```

6. Log in to the server and store a client access key.

```bash
$XENO login -u admin@xeno --server http://localhost:9000
```

For the seeded Docker server, use password `password`.

7. Add a local Ollama provider.

```bash
$XENO provider add --name local-ollama --type Ollama --endpoint http://127.0.0.1:11434
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

10. Start the client in the background.

```bash
$XENO start
```

Restart the background client after configuration or binary changes:

```bash
$XENO stop
$XENO start
```

11. Check status.

```bash
$XENO status
```

12. Open or print the local dashboard URL.

```bash
$XENO dashboard
$XENO dashboard open
```

13. Optionally enable the localhost OpenAI/Ollama-compatible inference listener.

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

Restart the client, then test local model listing:

```bash
$XENO restart
curl http://127.0.0.1:11434/v1/models
curl http://127.0.0.1:11434/api/tags
```

The listener is loopback-only and uses the client identity from `client.json`. If the WebSocket tunnel is unavailable, local API calls return provider-shaped `502` responses. If a local inference request exceeds `LocalInferenceServer.RequestTimeoutMs`, the listener cancels the tunneled request and returns a provider-shaped `504` response.

14. Watch logs.

```bash
$XENO logs --tail 100
$XENO logs -f
```

15. Stop the client.

```bash
$XENO stop
```

## Better Path: Start on Boot with systemd

Use a user service when the client should start when your user session starts.

1. Publish the client to a stable path.

```bash
dotnet publish src/Xeno.Client/Xeno.Client.csproj -c Release -o "$HOME/.xeno/bin"
```

2. Create the systemd user directory.

```bash
mkdir -p "$HOME/.config/systemd/user"
```

3. Create `~/.config/systemd/user/xeno-client.service`.

```ini
[Unit]
Description=Xeno Client
After=network-online.target

[Service]
Type=simple
ExecStart=%h/.xeno/bin/xeno start --foreground --worker
Restart=always
RestartSec=5

[Install]
WantedBy=default.target
```

4. Enable and start the service.

```bash
systemctl --user daemon-reload
systemctl --user enable --now xeno-client.service
```

5. Allow user services to start at boot without an interactive login if your distribution supports lingering.

```bash
loginctl enable-linger "$USER"
```

6. Monitor the service.

```bash
systemctl --user status xeno-client.service
journalctl --user -u xeno-client.service -f
```

7. Restart the service after configuration or binary changes.

```bash
systemctl --user restart xeno-client.service
```

8. Stop or remove it if needed.

```bash
systemctl --user disable --now xeno-client.service
```

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

Use the local dashboard to create, update, and delete provider endpoints and advertised models, inspect local request history, review host/provider telemetry, read logs, and manage local settings. Provider/model changes made through the dashboard or CLI are saved to `~/.xeno/client.json` and request an inventory update when the client is connected. If you edit `client.json` directly, run `$XENO reload` or use the Settings page reload action so the connected server receives the updated provider/model inventory.

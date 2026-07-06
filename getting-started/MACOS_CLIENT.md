# Xeno Client on macOS

This guide installs and runs the Xeno client on macOS.

## Defaults

- Client settings: `~/.xeno/client.json`
- Client state: `~/.xeno/state.json`
- Local dashboard: `http://127.0.0.1:9343`
- Default cloud WebSocket URL: `ws://app.xenocloud.ai:9000/ws/v1/clients`
- Local Docker server WebSocket URL: `ws://localhost:9000/ws/v1/clients`
- Local Docker server REST URL: `http://localhost:9000`
- Dashboard login for seeded Docker server: `admin@xeno` / `password`
- Default seeded client access key: `default`

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

11. Check status.

```bash
$XENO status
```

12. Open or print the local dashboard URL.

```bash
$XENO dashboard
$XENO dashboard open
```

13. Watch logs.

```bash
$XENO logs --tail 100
$XENO logs -f
```

14. Stop the client.

```bash
$XENO stop
```

## Better Path: Start on Boot with launchd

Use a LaunchAgent when the client should start when you log in.

1. Publish the client to a stable path.

```bash
dotnet publish src/Xeno.Client/Xeno.Client.csproj -c Release -o "$HOME/.xeno/bin"
```

2. Create the LaunchAgents directory.

```bash
mkdir -p "$HOME/Library/LaunchAgents"
```

3. Create `~/Library/LaunchAgents/ai.xenocloud.client.plist`.

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

Replace `/Users/YOUR_USER` with your actual home directory.

4. Load and start the LaunchAgent.

```bash
launchctl load "$HOME/Library/LaunchAgents/ai.xenocloud.client.plist"
launchctl start ai.xenocloud.client
```

5. Monitor the client.

```bash
$HOME/.xeno/bin/xeno status
$HOME/.xeno/bin/xeno logs --tail 100
tail -f "$HOME/.xeno/logs/launchd.out.log"
```

6. Stop or remove it if needed.

```bash
launchctl stop ai.xenocloud.client
launchctl unload "$HOME/Library/LaunchAgents/ai.xenocloud.client.plist"
```

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

Use the local dashboard to create, update, and delete provider endpoints and advertised models, inspect local request history, review host/provider telemetry, read logs, and manage local settings. Provider/model changes made through the dashboard or CLI are saved to `~/.xeno/client.json` and request an inventory update when the client is connected. If you edit `client.json` directly, run `$XENO reload` or use the Settings page reload action so the connected server receives the updated provider/model inventory.

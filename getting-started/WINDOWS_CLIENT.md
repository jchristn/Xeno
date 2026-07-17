# Xeno Client on Windows

This guide installs and runs the Xeno client on Windows.

## Defaults

- Client settings: `%USERPROFILE%\.xeno\client.json`
- Client state: `%USERPROFILE%\.xeno\state.json`
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

```powershell
git clone https://github.com/jchristn/Xeno.git
cd Xeno
```

## Install the Global Tool

1. Build and install `xeno` as a .NET global tool.

```powershell
.\install-tool.bat
```

2. Confirm the tool is on your path.

```powershell
xeno help
```

3. Remove the global tool later if needed.

```powershell
.\remove-tool.bat
```

## Easy Path: Build, Configure, Start, Monitor

1. Build the solution.

```powershell
dotnet build .\src\Xeno.sln
```

2. Build the local dashboard assets if you are running from source.

```powershell
cd dashboard
npm install
npm run build:local
cd ..
```

3. Initialize the client settings file.

```powershell
xeno init
```

4. If you are connecting to a local Docker server, set the server URLs.

```powershell
xeno server set-url ws://localhost:9000/ws/v1/clients
xeno server set-rest-url http://localhost:9000
```

5. Log in to the server and store a client access key.

```powershell
xeno login -u admin@xeno --server http://localhost:9000
```

For the seeded Docker server, use password `password`.

6. Add a local Ollama provider.

```powershell
xeno provider add --name local-ollama --type Ollama --endpoint http://127.0.0.1:11434
```

7. Advertise a completion model.

```powershell
xeno model add --provider local-ollama --name llama3.2:latest --type completion
```

8. Review the client configuration.

```powershell
xeno config show
xeno inventory
```

9. Start the client in the background.

```powershell
xeno start
```

Restart the background client after configuration or binary changes:

```powershell
xeno stop
xeno start
```

10. Check status.

```powershell
xeno status
xeno ping
```

11. Open the local Xeno Client dashboard.

```powershell
xeno dashboard open
```

12. Optionally enable the localhost OpenAI/Ollama-compatible inference listener.

Edit `%USERPROFILE%\.xeno\client.json` and add:

```json
"LocalInferenceServer": {
  "Enabled": true,
  "Host": "127.0.0.1",
  "Port": 11434,
  "EnableCors": true
}
```

Restart the client, then test local model listing:

```powershell
xeno stop
xeno start
curl http://127.0.0.1:11434/v1/models
curl http://127.0.0.1:11434/api/tags
```

The listener is loopback-only and uses the client identity from `client.json`. If the WebSocket tunnel is unavailable, local API calls return provider-shaped `502` responses.

13. Watch logs.

```powershell
xeno logs --tail 100
xeno logs -f
```

14. Stop the client.

```powershell
xeno stop
```

## Optional: Run the Windows Tray App

The tray app is useful on a Windows workstation where the client should be easy to start, stop, inspect, and reopen without keeping a terminal nearby. It uses the `xeno.exe` built beside it, so build the solution before launching the tray from source.

1. Build the tray app and its sibling client executable.

```powershell
dotnet build .\src\Xeno.sln
```

2. Start the tray app.

```powershell
Start-Process .\src\Xeno.Tray\bin\Debug\net10.0\xeno-tray.exe
```

3. Use the tray icon menu to start or stop the Xeno client, open the local dashboard, show client statistics, or quit the tray app.

To restart a tray-managed client after configuration changes, select **Stop Client**, then select **Start Client** from the tray icon menu.

The statistics window uses the normal Windows title bar close button. If you rebuild while the tray app or the client it started is still running from `src\Xeno.Tray\bin\Debug\net10.0`, stop both processes first so the build can replace `xeno-tray.exe`, `xeno.exe`, and their referenced assemblies.

## Better Path: Start on Boot

Use a scheduled task to run the foreground worker when you log in. This lets Task Scheduler supervise the long-running client process directly instead of supervising a short `xeno start` command that exits after spawning a child process.

1. Confirm `xeno` works from a new PowerShell window.

```powershell
xeno status
```

2. Create the scheduled task.

```powershell
$xenoPath = (Get-Command xeno).Source
$action = New-ScheduledTaskAction -Execute $xenoPath -Argument "start --foreground --worker"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName "Xeno Client" -Action $action -Trigger $trigger -Settings $settings -Description "Start Xeno Client at user logon"
```

3. Start it immediately.

```powershell
Start-ScheduledTask -TaskName "Xeno Client"
```

4. Confirm it connected.

```powershell
xeno status
xeno logs --tail 50
```

5. Restart the scheduled client after configuration or binary changes.

```powershell
Stop-ScheduledTask -TaskName "Xeno Client"
Start-ScheduledTask -TaskName "Xeno Client"
```

6. Remove the task if needed.

```powershell
Unregister-ScheduledTask -TaskName "Xeno Client" -Confirm:$false
```

## Manage the Client

Use these commands during normal operations:

```powershell
xeno status
xeno config show
xeno provider list
xeno provider update --name local-ollama --endpoint http://127.0.0.1:11434 --timeout 300000
xeno model list
xeno model update --provider local-ollama --name llama3.2:latest --type completion
xeno reload
xeno dashboard
xeno dashboard open
xeno logs --tail 100
xeno stop
xeno reset
```

Use the local dashboard to create, update, and delete provider endpoints and advertised models, inspect local request history, review host/provider telemetry, read logs, and manage local settings. Provider/model changes made through the dashboard or CLI are saved to `%USERPROFILE%\.xeno\client.json` and request an inventory update when the client is connected. If you edit `client.json` directly, run `xeno reload` or use the Settings page reload action so the connected server receives the updated provider/model inventory.

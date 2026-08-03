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

## Build and Configure

Do this once before choosing how the client should start.

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
xeno provider add --name local-ollama --type Ollama --endpoint http://127.0.0.1:11434 --allow-model-management
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

9. Optionally enable the localhost OpenAI/Ollama-compatible inference listener.

Edit `%USERPROFILE%\.xeno\client.json` and add:

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

Run the client yourself. This is the simplest way and is best for first-run validation, configuration changes, and development.

1. Start the client in the background. It runs until you stop it or the session ends.

```powershell
xeno start
```

Or run it attached to the terminal (press `Ctrl+C` to stop):

```powershell
xeno start --foreground --worker
```

2. Check status and open the local dashboard.

```powershell
xeno status
xeno ping
xeno dashboard open
```

3. Watch logs.

```powershell
xeno logs --tail 100
xeno logs -f
```

4. Restart after configuration or binary changes, or stop the client.

```powershell
xeno restart
xeno stop
```

To have the client start automatically, use **Start On Login** (when you sign in) or **Start On Boot** (headless, no sign-in).

### Run the Windows tray app

The tray app is a convenient interactive option on a Windows workstation, letting you start, stop, inspect, and reopen the client without keeping a terminal nearby. It uses the `xeno.exe` built beside it, so build the solution first.

1. Build the tray app and its sibling client executable.

```powershell
dotnet build .\src\Xeno.sln
```

2. Start the tray app.

```powershell
Start-Process .\src\Xeno.Tray\bin\Debug\net10.0\xeno-tray.exe
```

3. Use the tray icon menu to start or stop the Xeno client, open the local dashboard, show client statistics, or quit the tray app. To restart a tray-managed client after configuration changes, select **Stop Client**, then select **Start Client**.

The statistics window uses the normal Windows title bar close button. If you rebuild while the tray app or the client it started is still running from `src\Xeno.Tray\bin\Debug\net10.0`, stop both processes first so the build can replace `xeno-tray.exe`, `xeno.exe`, and their referenced assemblies.

## Start On Login

Start the client automatically when you sign in, using a scheduled task with an at-logon trigger. It runs in your interactive session, with access to your profile and GPU, and stops when you sign out. The task supervises the long-running foreground worker directly.

1. Confirm `xeno` works from a new PowerShell window, and stop any client you started interactively so that two workers do not register under the same client ID.

```powershell
xeno status
xeno stop
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

This task runs in your interactive session when you sign in. To keep it running with no one signed in, use **Start On Boot**.

## Start On Boot

Start the client at machine boot with no interactive sign-in, using a scheduled task with an at-startup trigger set to run whether or not a user is logged on — the right choice for a dedicated, headless GPU host. Run PowerShell as Administrator.

Because the task runs outside your interactive session, it must run as an account that has your Xeno configuration and GPU access. Run it as your own account with stored credentials (recommended, so it uses your `%USERPROFILE%\.xeno\client.json`), or as `SYSTEM`.

1. Stop any client you started interactively, and any task from **Start On Login**, so that two workers do not register under the same client ID.

```powershell
xeno stop
Stop-ScheduledTask -TaskName "Xeno Client" -ErrorAction SilentlyContinue
```

2. Create the task to run at startup as your account, whether or not you are signed in. Replace `YOUR_PASSWORD`.

```powershell
$xenoPath = (Get-Command xeno).Source
$action = New-ScheduledTaskAction -Execute $xenoPath -Argument "start --foreground --worker"
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName "Xeno Client (Boot)" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -User "$env:USERDOMAIN\$env:USERNAME" -Password "YOUR_PASSWORD" -Description "Start Xeno Client at boot"
```

To run as `SYSTEM` instead (no password), replace the `-User`/`-Password` arguments with a principal:

```powershell
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "Xeno Client (Boot)" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Start Xeno Client at boot"
```

3. Start it immediately and confirm.

```powershell
Start-ScheduledTask -TaskName "Xeno Client (Boot)"
xeno status
```

4. Remove the task if needed.

```powershell
Unregister-ScheduledTask -TaskName "Xeno Client (Boot)" -Confirm:$false
```

Config note: `SYSTEM` has its own profile, so its `client.json` is under `C:\Windows\System32\config\systemprofile\.xeno\client.json`, not your user profile. Either run the task as your own account (as above) so it uses `%USERPROFILE%\.xeno\client.json`, or copy your configured `client.json` into the `SYSTEM` profile path.

GPU note: a task that starts at boot runs in a non-interactive session (session 0). GPU access from `SYSTEM`/session 0 can be restricted by the driver. If the client cannot reach the GPU, run the task as a real user account with GPU access and "run whether user is logged on or not," and verify with `xeno status` and `xeno logs --tail 50` after a reboot.

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
xeno restart
xeno stop
xeno reset
```

If you registered a scheduled task from **Start On Login** or **Start On Boot**, do not use `xeno restart` to cycle it. `xeno restart` stops the worker the task launched and then starts its own detached client, which the task does not supervise, so you can end up with two workers under one client ID and a task that reports itself idle while a client is still connected. Use `Stop-ScheduledTask` and `Start-ScheduledTask` instead. `xeno stop` is the graceful shutdown — it asks the worker to exit and lets it clean up — but the task will not run again until its next trigger or a manual `Start-ScheduledTask`, while `Stop-ScheduledTask` terminates the worker outright and can leave `%USERPROFILE%\.xeno\state.json` describing a connection that no longer exists. The remaining commands are safe under a task: the read-only ones above, and `xeno reload`, which flags the running worker to re-advertise its inventory without touching the process.

Use the local dashboard to create, update, and delete provider endpoints and advertised models, inspect local request history, review host/provider telemetry, read logs, and manage local settings. Provider/model changes made through the dashboard or CLI are saved to `%USERPROFILE%\.xeno\client.json` and request an inventory update when the client is connected. If you edit `client.json` directly, run `xeno reload` or use the Settings page reload action so the connected server receives the updated provider/model inventory.

# Xeno Client Getting Started

Xeno v0.1.0 is an alpha release. Client commands, configuration shape, and server workflows are subject to change.

This guide walks through configuring the `xeno` client for a specific Xeno server, adding credentials, advertising provider endpoints, adding models, and connecting to the control plane.

## Prerequisites

- A running Xeno server.
- A client credential access key and secret key, or a user account that can log in and receive a client credential.
- At least one model provider reachable from the machine running `xeno`, such as Ollama, OpenAI, Gemini, vLLM, or another OpenAI-compatible endpoint.

The client stores its settings in:

```text
~/.xeno/client.json
```

Runtime connection status is stored in:

```text
~/.xeno/state.json
```

For sandboxed testing, set `XENO_CLIENT_HOME` to override the local client settings directory.

## Build the Client

From the repository root:

```powershell
dotnet build src/Xeno.sln
```

The debug build emits the client as:

```text
src/Xeno.Client/bin/Debug/net10.0/xeno.exe
```

On macOS and Linux, the executable name is `xeno`.

## Install the Global Tool

On Windows, install `xeno` as a .NET global tool from the repository root:

```powershell
install-tool.bat
```

The install script removes any existing `xeno` global tool installation, rebuilds the client from source, packs the tool, and installs it globally.

Remove the global tool when needed:

```powershell
remove-tool.bat
```

## Initialize Settings

Create the default client settings file:

```powershell
xeno init
```

Restore the client to factory defaults when you want to clear local configuration and runtime state:

```powershell
xeno reset
```

The command prompts you to type `RESET`. For scripts or disposable development environments:

```powershell
xeno reset --force
```

Reset recreates `~/.xeno/client.json` with default settings and removes `~/.xeno/state.json`.

Check the formatted configuration:

```powershell
xeno config show
```

The formatted configuration includes server settings, credential status,
local dashboard settings, configured provider endpoints, and each model
advertised by those providers.

Use JSON only when scripting:

```powershell
xeno --json config show
```

Enable or disable colored output:

```powershell
xeno config colors on
xeno config colors off
```

## Optional Local Dashboard

The background client can host a local web dashboard for the machine running
`xeno`. It shows connection state, provider endpoints, advertised models, local
request history, request details, request activity charts, and local statistics.
The Providers and Models pages let you create, edit, and delete local provider
endpoints and model advertisements without opening `client.json`. The Requests
page persists local request history in SQLite, including headers, bounded bodies,
telemetry, and timeline data for requests handled by this client.

The dashboard is enabled by default. The default settings in `~/.xeno/client.json` are:

```json
{
  "LocalDashboard": {
    "Enabled": true,
    "Host": "127.0.0.1",
    "Port": 9343,
    "OpenBrowserOnStart": false,
    "RequireLocalToken": true,
    "Database": {
      "Filename": "~/.xeno/local-dashboard.db",
      "ConnectionString": null
    },
    "HistoryRetention": {
      "MaxEntries": 1000,
      "MaxBodyBytes": 65536,
      "PersistBodies": true
    }
  }
}
```

`Host` defaults to `127.0.0.1`. Setting it to `0.0.0.0` is allowed, but that is
an intentional all-interface bind and may expose the dashboard beyond the local
machine depending on firewall and network policy. When `0.0.0.0` is used, the
CLI and dashboard display a warning.

The local dashboard stores request history in SQLite so it can support analytics
over requests processed by this client. Change `LocalDashboard.Database.Filename`
to move the database. `ConnectionString`, when supplied, overrides `Filename`.

The local dashboard currently exposes these client-local pages:

- Home: connectedness, local request KPIs, and requests over time.
- Provider Endpoints: create, edit, test, and delete configured provider endpoints.
- Advertised Models: create, edit, filter, and delete local model advertisements.
- Requests: inspect local request history, timing, headers, bodies, stream counters, and failures.
- Telemetry: inspect current CPU, memory, disk, and GPU telemetry collected by this client.
- Provider Telemetry: inspect current runtime telemetry from Ollama, vLLM, and other configured provider endpoints.
- Logs: read recent client log lines by severity and tail length.
- Telemetry Analytics: analyze persisted telemetry over Last Hour, Last Day, Last Week, and Last Month.
- Provider Telemetry Analytics: analyze persisted provider runtime telemetry over Last Hour, Last Day, Last Week, and Last Month.
- Settings: edit local dashboard listener, token, SQLite database, and retention settings.

The archived client telemetry implementation plan and remaining future
server-upload considerations are tracked in
[archive/CLIENT_TELEMETRY.md](archive/CLIENT_TELEMETRY.md). Provider runtime
telemetry expansion is tracked in
[ADDITIONAL_TELEMETRY.md](ADDITIONAL_TELEMETRY.md).

These settings require restarting the background client:

- `Enabled`
- `Host`
- `Port`
- `Database.Filename`
- `Database.ConnectionString`

Start the client:

```powershell
xeno start
```

When the local dashboard is listening, `xeno start` and `xeno connect` print:

```text
Local dashboard : http://127.0.0.1:9343
```

You can also inspect or open it later:

```powershell
xeno status
xeno dashboard
xeno dashboard open
```

Startup errors and the dashboard URL are written to `xeno logs`. Use this when
the dashboard does not start, the port is already in use, or the browser shows a
blank page:

```powershell
xeno logs --tail 50
```

The local dashboard API uses a per-process local token injected into the page.
Provider credentials, server secret keys, REST tokens, authorization headers,
cookies, and credential-like headers are redacted from API responses and stored
history.

Set `LocalDashboard.Enabled` to `false` to disable the local dashboard.

## Local Telemetry

The background client collects machine telemetry when `Telemetry.Enabled` is
true. Telemetry is stored in the same local SQLite database used by the local
dashboard and is retained according to `Telemetry.RetentionDays`.

Default settings:

```json
{
  "Telemetry": {
    "Enabled": true,
    "CollectionIntervalMs": 15000,
    "RetentionDays": 30,
    "PruneIntervalMinutes": 60,
    "PersistRawSnapshot": true,
    "Gpu": {
      "Enabled": true,
      "NvidiaDcgmExporterUrl": null,
      "CommandTimeoutMs": 5000
    },
    "ProviderTelemetry": {
      "Enabled": true,
      "CollectionIntervalMs": 15000,
      "RequestTimeoutMs": 5000,
      "ConcurrentProviderLimit": 4,
      "DiscoverMetricsEndpoints": true,
      "PersistRawMetrics": false,
      "RawMetricsMaxBytes": 262144,
      "CollectModelDetails": true,
      "CollectProcessCorrelation": true,
      "CollectCloudHeaderTelemetry": true
    }
  }
}
```

`CollectionIntervalMs` is clamped to at least 5000 ms. `RetentionDays` is
clamped from 1 to 365 days. GPU telemetry tries a configured NVIDIA DCGM
exporter first, then falls back to `nvidia-smi` when it is available.

Provider telemetry is collected from configured provider endpoints on the same
local schedule. Ollama uses `/api/version`, `/api/tags`, and `/api/ps` when
available. vLLM and OpenAI-compatible runtimes use `/v1/models` and `/metrics`
when exposed. Generic OpenAI-compatible, LocalAI, llama.cpp, LM Studio, and TGI
endpoints degrade to the telemetry they expose. OpenAI and Gemini cloud
providers do not expose host internals; Xeno records only safe reachability,
model inventory, and response metadata where available. Raw Prometheus metrics
are not persisted unless `ProviderTelemetry.PersistRawMetrics` is enabled, and
stored raw metrics are bounded by `RawMetricsMaxBytes`.

### Local Dashboard API

The local dashboard is served by the `xeno` background process. Its API is
local to the client machine and uses a per-process token injected into the page
when `RequireLocalToken` is true.

Current routes:

```text
GET    /api/v1/state
GET    /api/v1/statistics
GET    /api/v1/database
GET    /api/v1/providers
POST   /api/v1/providers
PUT    /api/v1/providers/{name}
DELETE /api/v1/providers/{name}
POST   /api/v1/providers/{name}/test
GET    /api/v1/models
POST   /api/v1/models
PUT    /api/v1/models/{provider}/{name}
DELETE /api/v1/models/{provider}/{name}
GET    /api/v1/requests
GET    /api/v1/requests/summary
GET    /api/v1/requests/analytics/overview
GET    /api/v1/requests/{id}
DELETE /api/v1/requests/{id}
POST   /api/v1/requests/bulk
GET    /api/v1/telemetry/status
GET    /api/v1/telemetry/latest
POST   /api/v1/telemetry/collect
POST   /api/v1/telemetry/history/search
POST   /api/v1/telemetry/history/rollups
GET    /api/v1/telemetry/history/{id}
DELETE /api/v1/telemetry/history/{id}
POST   /api/v1/telemetry/history/delete
GET    /api/v1/telemetry/providers/status
GET    /api/v1/telemetry/providers/latest
POST   /api/v1/telemetry/providers/collect
POST   /api/v1/telemetry/providers/history/search
POST   /api/v1/telemetry/providers/history/rollups
GET    /api/v1/telemetry/providers/history/{id}
DELETE /api/v1/telemetry/providers/history/{id}
POST   /api/v1/telemetry/providers/history/delete
GET    /api/v1/logs
GET    /api/v1/settings
PUT    /api/v1/settings
```

## Point the Client at a Server

Set the WebSocket endpoint used by `xeno connect`:

```powershell
xeno server set-url ws://localhost:9000/ws/v1/clients
```

Set the REST endpoint used by `xeno login`:

```powershell
xeno server set-rest-url http://localhost:9000
```

For a remote server, replace the host and scheme:

```powershell
xeno server set-url wss://xeno.example.com/ws/v1/clients
xeno server set-rest-url https://xeno.example.com
```

## Authenticate

The WebSocket connection requires a client access key and secret key. User passwords are accepted only by `xeno login` and are not stored by the client.

### Option 1: Login with a User

```powershell
xeno login -u admin@xeno --tenant ten_default --server http://localhost:9000
```

The command prompts for a password and stores only the returned client credential.

For the default development seed data:

```text
username: admin@xeno
password: password
tenant:   ten_default
```

### Option 2: Store an Existing Client Credential

```powershell
xeno set-auth --access-key default --secret-key default
```

The default development credential uses:

```text
access key: default
secret key: default
```

There is no Xeno client API key mode. Use a client access key and secret key.

Clear stored authentication when needed:

```powershell
xeno set-auth --clear
```

## Add Provider Endpoints

List configured provider endpoints:

```powershell
xeno provider list
```

Add a local Ollama endpoint:

```powershell
xeno provider add --name local-ollama `
                  --type Ollama `
                  --endpoint http://127.0.0.1:11434
```

Add an OpenAI-compatible endpoint such as vLLM:

```powershell
xeno provider add --name vllm `
                  --type OpenAI `
                  --endpoint http://127.0.0.1:8000 `
                  --credential <upstream-token>
```

The `--type` value is the upstream API type, such as `Ollama`, `OpenAI`, `Gemini`, `vLLM`, or `OpenAICompatible`.
The optional provider credential is sent to the upstream provider as a bearer token.

Adding a provider with an existing name or endpoint is ignored so the same provider is not duplicated.

Test a configured provider endpoint:

```powershell
xeno provider test --name local-ollama
```

Delete a configured provider endpoint:

```powershell
xeno provider delete --name local-ollama
```

Aliases:

```powershell
xeno provider ls
xeno provider rm --name local-ollama
xeno provider remove --name local-ollama
xeno provider remote --name local-ollama
```

## Add Models

List models advertised by this client:

```powershell
xeno model list
```

Alias:

```powershell
xeno model ls
```

Add an Ollama model:

```powershell
xeno model add --provider local-ollama --name llama3.2:latest
```

Add a model exposed by an OpenAI-compatible endpoint:

```powershell
xeno model add --provider vllm --name meta-llama/Llama-3.1-8B-Instruct
```

By default, `xeno model add` records the model as completion-capable. Use `--type completion`, `--type embedding`, or `--type both` to explicitly control the capabilities advertised for that model.
The `--provider` value is the name of an existing provider endpoint from `xeno provider list`; use `xeno provider add` to configure endpoint URLs and API types.
`xeno model add` does not accept endpoint URLs; endpoint URL, upstream credential, and API type belong to `xeno provider add`.

Equivalent explicit capability forms:

```powershell
xeno model add --provider local-ollama --name llama3.2:latest --type completion
xeno model add --provider local-ollama --name all-minilm --type embedding
xeno model add --provider local-ollama --name hybrid-model --type both
xeno model add --provider local-ollama --name hybrid-model --completions true --embeddings true
```

Delete a model from every configured provider that advertises it:

```powershell
xeno model delete --name llama3.2:latest
```

Delete a model only from one provider:

```powershell
xeno model delete --provider local-ollama --name llama3.2:latest
```

Aliases:

```powershell
xeno model rm --name llama3.2:latest
xeno model remove --name llama3.2:latest
xeno model remote --name llama3.2:latest
```

## Verify Inventory and Status

Show the hardware and model inventory that the client will advertise:

```powershell
xeno inventory
```

Show configuration and connection state:

```powershell
xeno status
```

`xeno status` includes whether the client is currently connected, the last connected/disconnected times, heartbeat time, server-message time, and disconnect reason.

Use JSON for automation:

```powershell
xeno --json status
```

## Connect to the Xeno Server

Start the persistent WebSocket client in the background:

```powershell
xeno connect
```

Alias:

```powershell
xeno start
```

`xeno connect` starts a background process and returns after the client is
registered with the server. The client automatically reconnects after
disconnects. While it is running, `xeno status` shows connection, registration,
background-process, heartbeat, and last-disconnect state.

Run the WebSocket loop in the current terminal when you want foreground logs:

```powershell
xeno connect --foreground
```

Control how long the background command waits for registration:

```powershell
xeno connect --timeout 30000
```

View or follow client logs:

```powershell
xeno logs
xeno logs -f
xeno logs --tail 200
xeno logs --errors
```

Log lines include UTC timestamp and severity, for example:

```text
2026-06-26 01:33:45.123456Z [INFO] Xeno client registered.
```

Stop the background client:

```powershell
xeno stop
xeno disconnect
```

Use `Ctrl+C` only when the client is running with `xeno connect --foreground`.

## Common Help Commands

```powershell
xeno help
xeno config help
xeno server help
xeno auth help
xeno provider help
xeno model help
xeno logs help
xeno stop help
```

Invalid commands print formatted command-specific usage blocks instead of single-line usage strings.

## Client Configuration Shape

The current settings file uses `Providers` for configured provider endpoints and
`ApiType` for the upstream API family:

```json
{
  "TenantId": "ten_default",
  "Name": "WORKSTATION",
  "UseColors": true,
  "Server": {
    "Url": "ws://localhost:9000/ws/v1/clients",
    "AccessKey": "default",
    "SecretKey": "default"
  },
  "Providers": [
    {
      "Name": "local-ollama",
      "ApiType": "Ollama",
      "Endpoint": "http://127.0.0.1:11434",
      "TimeoutMs": 300000,
      "Models": [
        {
          "Name": "llama3.2:latest",
          "SupportsCompletions": true,
          "SupportsEmbeddings": false
        }
      ]
    }
  ]
}
```

Legacy `Runners` arrays and per-provider `Provider` fields are still read for
backward compatibility, but new output uses `Providers` and `ApiType`.

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
configured provider endpoints, and each model advertised by those providers.

Use JSON only when scripting:

```powershell
xeno --json config show
```

Enable or disable colored output:

```powershell
xeno config colors on
xeno config colors off
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

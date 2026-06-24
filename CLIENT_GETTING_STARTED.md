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

Check the formatted configuration:

```powershell
xeno config show
```

Use JSON only when scripting:

```powershell
xeno --json config show
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
                  --provider Ollama `
                  --endpoint http://127.0.0.1:11434
```

Add an OpenAI-compatible endpoint such as vLLM:

```powershell
xeno provider add --name vllm `
                  --provider OpenAI `
                  --endpoint http://127.0.0.1:8000 `
                  --credential <upstream-token>
```

The optional provider credential is sent to the upstream provider as a bearer token.

Test a configured provider endpoint:

```powershell
xeno provider test --name local-ollama
```

## Add Models

List models advertised by this client:

```powershell
xeno model list
```

Add an Ollama model:

```powershell
xeno model add --provider local-ollama --name llama3.2:latest
```

Add a model exposed by an OpenAI-compatible endpoint:

```powershell
xeno model add --provider vllm --name meta-llama/Llama-3.1-8B-Instruct
```

By default, `xeno model add` records the model as completion-capable. Edit `~/.xeno/client.json` directly if a model should also advertise embeddings before the CLI exposes a dedicated flag.

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

Start the persistent WebSocket client:

```powershell
xeno connect
```

The client automatically reconnects after disconnects. While it is running, `xeno status` in another terminal shows `Connected: true`.

Stop the client with `Ctrl+C`.

## Common Help Commands

```powershell
xeno help
xeno config help
xeno server help
xeno auth help
xeno provider help
xeno model help
```

Invalid commands print formatted command-specific usage blocks instead of single-line usage strings.

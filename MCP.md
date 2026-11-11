# Xeno MCP Server

The Xeno client is an MCP server. Agents such as Claude Code can use it to look at this machine's Xeno client and
the Xeno network, change routing, and ask any model on this machine or the network a question. Xeno's in-app chat
uses the same tools when a conversation has tools turned on.

## Two ways to connect

| Transport | How | Needs |
|---|---|---|
| stdio | The MCP client runs `xeno mcp` | Xeno installed. `xeno_chat` also needs the client running. |
| Streamable HTTP | `http://127.0.0.1:11436/mcp` (JSON-RPC at `/rpc`) | The client running with the MCP server turned on, and the MCP token |

`xeno mcp config` prints both setups for this machine. For Claude Code:

```sh
# stdio
claude mcp add xeno -- xeno mcp

# HTTP (turn the MCP server on in Settings first, then restart the client)
claude mcp add --transport http xeno http://127.0.0.1:11436/mcp --header "Authorization: Bearer $(xeno mcp token)"
```

## Settings

The HTTP server is off by default. Turn it on in any Settings editor (desktop app, terminal app, or local web
dashboard), or in `~/.xeno/client.json`:

```json
"Mcp": {
  "Enabled": true,
  "Host": "127.0.0.1",
  "Port": 11436,
  "RequireToken": true
}
```

- The host must be loopback (`127.0.0.1`, `localhost`, `::1`, or `[::1]`). Requests from other machines are refused.
- With `RequireToken` on (the default), every request needs `Authorization: Bearer <token>`. The token lives in
  `~/.xeno/mcp-token` (readable only by you on Linux and macOS) and stays the same across restarts.
  `xeno mcp token` prints it; `xeno mcp token --rotate` replaces it.
- Changes take effect when the client restarts.

## Tools

`xeno mcp tools` lists them. Read-only tools run right away in Xeno chat; the others ask first.

| Tool | Kind | What it does |
|---|---|---|
| `xeno_status` | read-only | Client status: running, connected, server, local dashboard and endpoint, routing, provider counts |
| `xeno_list_models` | read-only | Models on this machine and the Xeno network, and where each would go |
| `xeno_resolve_route` | read-only | Where a request for a model would go now, and why |
| `xeno_list_providers` | read-only | Configured providers, their models, and how they are shared. Never includes credentials |
| `xeno_provider_health` | read-only | Routing mode, route rules, and each provider's health, latency, load, and limit |
| `xeno_check_providers` | read-only | Probe every provider now |
| `xeno_request_history_query` | read-only | Recent local endpoint requests (metadata only) |
| `xeno_chat_history_query` | read-only | Chat turns, filtered by model, route, outcome, rating, source, text, and time |
| `xeno_feedback_query` | read-only | Ratings and comments on chat answers |
| `xeno_list_conversations` | read-only | Saved chat conversations |
| `xeno_get_conversation` | read-only | One conversation with its messages, tool calls, and ratings |
| `xeno_usage` | read-only | Usage analytics for served requests, the local endpoint, or chat, by range, group, metric, and model |
| `xeno_set_route_rule` | changes things | Send a model to a local provider or to the Xeno network |
| `xeno_remove_route_rule` | changes things | Remove a model's route rule |
| `xeno_start_client` | changes things | Start the client |
| `xeno_stop_client` | changes things | Stop the client |
| `xeno_chat` | changes things | Ask a model a question; saved as a conversation marked MCP. Not offered to Xeno's own chat |

Argument errors (a missing required argument, a bad value) come back as tool results with `isError: true` and a
message that says what to fix.

## Implementation

- Tools: `Xeno.Client.Tools.XenoToolCatalog` (one list for both transports and for in-app chat),
  `XenoChatToolHost` (in-app chat; leaves out `xeno_chat`), `XenoMcpTools` (registers on Voltaic servers).
- HTTP: `Xeno.Client.Mcp.XenoMcpHttpRuntime`, started with the client after the local dashboard.
- stdio: `xeno mcp` (`ProgramMcp.cs`). It reads chat history straight from the local database and sends
  `xeno_chat` through the running client's local API.
- Tests: `src/Test.Shared/McpTestSuites.cs`.

# Xeno Chat

Chat with any model this machine serves or the Xeno network offers, from every Xeno app: the local web dashboard,
the desktop app, the terminal app, the `xeno` CLI, the tray agent (Chat... opens the desktop app's Chat page), and
agents through the `xeno_chat` MCP tool. Answers stream as the model writes them. Every answer can be rated thumbs up
or down with a comment, and every chat turn is recorded in chat history with its model, route, timing, tokens, tool
calls, and rating.

## Where chat runs

Chat runs inside the client process, the only process with the network tunnel, the router, and the per-provider
concurrency limits. Each turn is sent as an OpenAI-shaped `/v1/chat/completions` request through the same router the
local inference endpoint uses:

- A model served by a provider on this machine goes straight to that provider, under the same concurrency limit as
  network requests that provider serves.
- Anything else goes to the Xeno network through the client's connection.
- A route rule's network fallback applies, and only before the first answer byte.

The local inference endpoint does not need to be turned on. The client and its local dashboard must be running; the
apps say so and offer to start the client when it is not.

## Choosing a model

Picking a model loads it on its provider right away (Ollama keeps it loaded for 30 minutes) and checks whether it can
use tools. New chats turn tools on when the model supports them; a model that rejects tools gets the message again
without them, and the conversation's tools are switched off. Streaming is on by default and can be turned off per app.
Every answer carries telemetry: time to first token, streaming, model, and tool time, tokens in, cached, out, and
reasoning, and tokens per second (the (i) next to an answer on the web, the line under each answer and the
Telemetry button in the terminal app). Every app shows "AI can make mistakes. Please check all work." under the
message box.

## When a model does not answer

Every app says so in the conversation, where the answer would be, with the reason. The reason is saved with the
conversation, so it is still there when the conversation is opened again later or in another app.

| Shown as | When |
|---|---|
| No response from the model | The model finished without writing an answer (or only reasoned) |
| The model timed out / The answer timed out | The provider, the Xeno network, or the client did not answer within its timeout |
| The model is unavailable | The provider is down or busy, the route has no provider, or the client is not connected to the Xeno network |
| The model returned an error | The model or provider returned an error, such as an unknown model |
| The answer was cut off | Part of the answer arrived, then the provider or connection failed; the partial answer is kept |
| Stopped | You pressed Stop |

The web apps show a red note under the answer, the desktop app a red line, the terminal app a `(!)` line, and the CLI
prints it on standard error (`xeno chat --json` adds `FailureKind`). Stream clients get the same in the `error` event's
`Failure` (`Kind`, `Reason`, `StatusCode`), and conversation messages carry it as `Failure`. A failure stand-in is never
sent back to the model.

## Storage

Conversations, messages, tool calls, ratings, and chat history are stored in the local dashboard database
(`~/.xeno/local-dashboard.db`). Nothing about them leaves the machine except the model request itself.

- Conversations stay until you delete them.
- Chat history rows hold no prompt or answer text, only metadata, and are kept for `Chat.HistoryRetentionDays`
  (default 30). Deleting a conversation keeps its history rows.
- One rating per answer; rating again replaces it.

## Tools

With tools on for a conversation, the model can call:

| Group | Tools | Asks first |
|---|---|---|
| Xeno | Xeno's status, model, routing, provider, history, and feedback tools (see [MCP.md](../MCP.md)) | route rules, start and stop the client |
| Files | `read_file`, `write_file`, `edit_file`, `multi_edit`, `delete_file`, `file_metadata`, `list_directory`, `manage_directory`, `glob`, `grep` | writing, editing, deleting, and folder changes |
| Processes | `run_process`, `process_start`, `process_output`, `process_list`, `process_stop` | running, starting, and stopping commands |
| Web | `web_retrieve`, and `web_search` when a provider is set | never |
| Planning | `plan_tasks`, `update_task` | never |
| Memory | `remember`, `forget`, `recall` | saving and forgetting |
| MCP servers | `mcp_<server>_<tool>` from each server in `Chat.McpServers` | unless the server marks the tool read-only or is trusted |

The file, process, web, planning, and memory tools are ported from Mux and keep Mux's names and arguments.

- **Working folder.** Each conversation has a working folder, shown in the chat header (the folder chip on the web, Folder in the desktop and terminal apps, `--cwd` and `/cwd` in the CLI). File and process tools resolve paths against it and refuse anything outside it, including through symbolic links. New chats use `Chat.DefaultWorkingDirectory`, or your home folder; the CLI uses the folder you run it from.
- **Processes** run without a shell unless the call sets `shell: true`, start with no input, are killed with their children on timeout (`Chat.ProcessTimeoutSeconds`, default 120) or Stop, and keep at most 8 background processes per conversation. Deleting a conversation stops its background processes.
- **Web.** `web_retrieve` refuses loopback and private network addresses unless `Chat.WebRetrieveAllowPrivateNetworks` is on, follows at most 5 redirects, and reads at most 5 MB without running scripts. `web_search` uses the provider in `Chat.WebSearch` (Tavily or You.com, with its API key).
- **Memory** is kept as Markdown files under `~/.xeno/memory`, per project (the Git repository or folder the conversation works in) or global.
- Tool results longer than `Chat.MaxToolOutputCharacters` (default 50000) are cut off.
- Read-only tools run right away. The rest wait for Allow or Deny; no answer within `Chat.ToolApprovalTimeoutSeconds` (default 120) denies the call. Limits: `Chat.MaxToolIterations` model calls (default 8) and `Chat.MaxToolCallsPerTurn` tool calls (default 16) per message; the last model call offers no tools so the model has to answer.
- Models that cannot use tools get none: Ollama models are checked when picked (the `tools` capability and a template or renderer that renders tools), and a model that rejects tools gets the message again without them.

### External MCP servers

```json
"McpServers": [
  { "Name": "docs", "Transport": "http", "Url": "http://127.0.0.1:8000/mcp", "Headers": { "Authorization": "Bearer ..." } },
  { "Name": "fs", "Transport": "stdio", "Command": "npx", "Arguments": ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"], "TrustReadOnlyTools": false, "TimeoutSeconds": 60 }
]
```

The client connects when it starts and after settings are saved; the web dashboard's Settings page shows each server's state and has a **Connect MCP servers** button. Header and environment values, and the web search API keys, appear as `configured` in settings responses.

## Settings

```json
"Chat": {
  "HistoryRetentionDays": 30,
  "MaxToolIterations": 8,
  "MaxToolCallsPerTurn": 16,
  "ToolApprovalTimeoutSeconds": 120,
  "DefaultSystemPrompt": null,
  "DefaultWorkingDirectory": null,
  "ProcessTimeoutSeconds": 120,
  "MaxToolOutputCharacters": 50000,
  "TaskPlanningEnabled": true,
  "MemoryEnabled": true,
  "WebRetrieveAllowPrivateNetworks": false,
  "WebSearch": { "Provider": "None", "TavilyApiKey": null, "YouApiKey": null, "MaxResults": 5 },
  "McpServers": []
}
```

Every Settings editor (desktop, terminal, web) has a Chat section. Changes apply to the next message.

## CLI

```sh
xeno chat --tools                          # interactive; /model, /models, /new, /tools on|off, /cwd, /system, /rate up|down, /exit
xeno chat --tools --cwd ~/Code/app "..."   # tools work in --cwd (default: the current folder)
xeno chat -m llama3.1:8b "Explain RAID 5"  # ask once; --json for scripts; reads a piped message too
xeno chat -c cnv_... "and RAID 6?"         # continue a conversation
xeno chat models | list | show <id> | delete <id>
xeno chat rate <message-id> up|down [comment]
xeno chat history [--model] [--route local|network] [--status] [--surface] [--rating up|down|none] [--limit]
xeno chat feedback [--rating up|down] [--comments]
```

## Local API

All routes are on the local dashboard (`http://127.0.0.1:9343` by default) and need the `X-Xeno-Local-Token` header.
The client publishes the URL and token for the CLI, desktop app, and terminal app in `~/.xeno/local-api.json`
(readable only by you on Linux and macOS) while it runs.

| Method and path | Purpose |
|---|---|
| `GET /api/v1/chat/models` | Models to chat with (this machine first, then the Xeno network); embedding models are left out |
| `POST /api/v1/chat/models/prepare` | `{ "model" }`: load the model on its provider (Ollama) and report `Loaded`, `SupportsTools`, `SupportsThinking`, `LoadMs` |
| `GET /api/v1/chat/tools` | Tools the model can use now, with `Group` and whether each asks first |
| `GET`, `POST /api/v1/chat/mcp-servers` | External MCP servers' connection state; POST connects again and refreshes their tools |
| `GET /api/v1/chat/conversations?search=&surface=&limit=` | List conversations |
| `POST /api/v1/chat/conversations` | Start one: `{ "model", "title", "systemPrompt", "toolsEnabled", "workingDirectory", "surface" }` (the folder must exist; empty means the default) |
| `GET`, `PUT`, `DELETE /api/v1/chat/conversations/{id}` | Read with messages, change, delete |
| `POST /api/v1/chat/conversations/{id}/messages` | Send `{ "content", "model", "stream", "surface" }`; events stream as server-sent events either way, and `stream: false` asks the model for the whole answer at once |
| `POST /api/v1/chat/turns/{id}/cancel` | Stop a running turn |
| `POST /api/v1/chat/turns/{id}/tool-calls/{toolCallId}/approval` | `{ "allow": true }` or false |
| `PUT`, `DELETE /api/v1/chat/messages/{id}/feedback` | Rate `{ "rating": 1 or -1, "comment" }`, or remove the rating |
| `GET /api/v1/chat/feedback?rating=&model=&route=&hasComment=&search=&fromUtc=&toUtc=&page=&pageSize=` | Query ratings |
| `GET`, `DELETE /api/v1/chat/history?model=&route=&provider=&status=&surface=&rating=&fallback=&conversationId=&search=&fromUtc=&toUtc=&page=&pageSize=` | Query or delete chat turns |
| `GET /api/v1/chat/history/{id}` | One turn with its tool calls |
| `GET /api/v1/chat/analytics?fromUtc=&toUtc=&bucketSeconds=` | Totals, percentiles, ratings, buckets, per-model breakdown (`bucketMinutes` still accepted). The apps use 60, 96, 84, and 120 buckets for the last hour, day, week, and month, like every Xeno chart |

`route` accepts `local` (any provider on this machine), `network`, or an exact `local:<provider>`. `rating=0` means
unrated.

### Stream events

Each event is `event: <name>` followed by `data: <json>`:

| Event | Data |
|---|---|
| `turn` | The turn started: `TurnId`, `ConversationId`, `MessageId` (the user's message) |
| `thinking` | Reasoning text (`Text`) |
| `chunk` | Answer text (`Text`) |
| `tool_call` | A tool call changed state (`ToolCall`, with `Approval` Pending when it needs Allow or Deny) |
| `error` | The turn failed: `Text` is the line to show, `Failure` has `Kind`, `Reason`, and `StatusCode` |
| `usage` | The finished turn (`Turn`) |
| `done` | End of stream (`Turn`, `MessageId` of the answer) |

## Server web dashboard

The Xeno server's dashboard has a **Chat** page (the same page as the local dashboard, without tools) and a **Chat
review** page with chat history, timing, and ratings. Tenant admins can switch the review to everyone in the tenant.

- Turns go through the server's network relay to a connected client, exactly like `/v1/chat/completions`: they appear
  in request history and charge the user's credits.
- Conversations, messages, ratings, and chat history are stored by the server per tenant and user, in the server's
  database (SQLite, PostgreSQL, MySQL, or SQL Server), separately from each machine's local store.
- The routes are under `/v1.0/api/chat/` (see `REST_API.md`) and need the `Chat` permission. Existing tenants get it
  for their TenantMember role when the server starts.

## Usage analytics

The Usage page, `xeno usage --dataset chat`, and `GET /api/v1/chat/analytics/usage` chart chat turns: outcomes, latency, time to first token, tokens, feedback, tool calls, and time by stage. Time waiting for someone to allow or deny a tool counts as Approval, not Tools, so a slow decision does not look like a slow tool. See [USAGE_ANALYTICS.md](USAGE_ANALYTICS.md).

## Implementation

- Tools: `src/Xeno.Client.Core/Tools/XenoChatToolHost.cs` (every tool chat offers), `Tools/Workspace` (the Mux tools, `WorkspacePath` confinement), and `Tools/Mcp` (`McpClientManager`).
- Client storage and API client: `src/Xeno.Client.Core/Chat` (`SqliteChatStore`, `XenoChatClient`) and
  `src/Xeno.Client/LocalInference/RoutedChatTransport.cs`.
- Local API: `src/Xeno.Client/LocalDashboard/LocalDashboardService.Chat.cs`.
- Front ends: `dashboard/src/local/chat/`, `src/Xeno.Desktop/Pages/Chat*.cs`, `src/Xeno.Tui/Pages/Chat*.cs`,
  `src/Xeno.Client/ProgramChat.cs`.
- Shared engine and models: `src/Xeno.Core/Chat` (used by the client and the server).
- Server: `src/Xeno.Core/Database/DbChatMethods.cs` (one store for all four databases), `src/Xeno.Server/XenoServer.Chat.cs`,
  `src/Xeno.Server/Routes/ChatRoutes.cs`, and `dashboard/src/chat/` (shared chat page, server views).
- Tests: `src/Test.Shared/ChatTestSuites.cs`, `McpTestSuites.cs`, `TuiTestSuites.cs`, `SharedTestSuites.Chat.cs`
  (server, run against every database with `--db-provider`), and both dashboard Playwright suites.

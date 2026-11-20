# Chat Plan

Status: Approved. Phases 1-5 done; Phase 6 docs done, UI pass partly done (2026-11-10)
Owner: unassigned
Last updated: 2026-11-10

## Summary

Xeno routes model requests but gives its users no way to talk to a model. This plan adds Wilson-style chat (`~/Code/Wilson`) to every Xeno surface: the local web dashboard, the desktop app, the terminal app, the `xeno` CLI, the tray agent, and the Xeno server's web dashboard. A user picks any model the catalog offers, local or network, and chats with it. Answers stream, conversations persist, every assistant message can be rated thumbs up or down with a free-text comment, and every chat turn is recorded in a queryable, charted request history.

The local Xeno client also becomes an MCP server. It exposes Xeno's own tools (models, routing, status, request history, chat) over MCP, so Claude Code and other agents can call them, and the in-app chat model can call the same tools during a conversation. Read-only tools run automatically; tools that change something ask first.

## Decisions

Made with the product owner on 2026-11-09.

| # | Decision |
|---|---|
| C1 | Chat is available in the local web dashboard, the desktop app, the terminal app, the CLI (`xeno chat`), the tray agent (a menu item opening the desktop Chat page), and the Xeno server web dashboard. |
| C2 | The local Xeno client is an MCP **server** exposing Xeno's own tools. It does not connect to external MCP servers. |
| C3 | The in-app chat model can call the client's own MCP tools. Approval is per tool: read-only tools run automatically; tools that change state require Allow or Deny in the chat. |
| C4 | Local conversations, feedback, and chat request history are stored only in the local client database under `~/.xeno`. Nothing about them leaves the machine except the model request itself. |
| C5 | Server-dashboard conversations, feedback, and history are stored by the server per user, separately from local stores. No tools are offered there. |
| C6 | Feedback is thumbs up, thumbs down, and optional free text, attached to one assistant message, following Wilson's model with AssistantHub-style analytics. |
| C7 | Request history follows AssistantHub: rich filters, paging, enforced retention, and links between turns, tool calls, and feedback. |
| C8 | Chat inference runs in the client process. Other front ends drive it through the local API. |

## Design

### Where chat runs

Only the running client process (`Xeno.Client`) holds the websocket tunnel, the router, the provider concurrency gate, and the health monitor. Chat inference therefore runs there. A `ChatEngine` in `Xeno.Client` owns the turn loop; the other front ends drive it through new endpoints on the local dashboard API, using one typed `XenoChatClient` in `Xeno.Client.Core` (exposed through `XenoWorkspace` for the desktop and terminal apps, and called directly by the CLI). When the client is not running, chat pages show "Start the client to chat" with a Start button, following `XenoCommandAvailability`.

Each turn is sent as an OpenAI-shaped `/v1/chat/completions` request through `LocalInferenceRouter`, exactly as the listener would route it: local providers through `LocalProviderProxy` with a `ProviderConcurrencyGate.Shared` lease, network models through the tunnel. The listener does not need to be enabled. Chat uses only the OpenAI shape, because Ollama serves it natively, vLLM and OpenAI-compatible providers speak it, and it carries tool calls. All payloads are named DTOs (`ChatCompletionRequestDto`, `ChatCompletionChunkDto`, `ChatToolCallDto`, ...), per AGENTS.md.

### Data model (local SQLite, `Xeno.Client.Core/Chat`)

Tables are added to the existing local dashboard database with hand-written SQL, matching `SqliteLocalRequestHistoryMethods`.

- `chat_conversations`: Id (PrettyId `cnv_`), Title, Model, SystemPrompt, ToolsEnabled, Pinned, CreatedUtc, UpdatedUtc.
- `chat_messages`: Id (`msg_`), ConversationId, Sequence, Role (System, User, Assistant, Tool), Content, Thinking, Model, Route (`local:<provider>` or `network`), Fallback, PromptTokens, CompletionTokens, TimeToFirstTokenMs, TotalMs, TurnId, ToolCallId, CreatedUtc.
- `chat_tool_calls`: Id, TurnId, MessageId, ToolName, ArgumentsJson, ResultJson (truncated), Approval (Auto, Allowed, Denied, TimedOut), Success, DurationMs, Error.
- `chat_feedback`: Id (`fbk_`), ConversationId, MessageId, Rating (+1 or -1), Comment, Model, Route, a snapshot of the message's token and timing fields, CreatedUtc, UpdatedUtc. One row per message; rating again replaces it.
- `chat_turns` (request history): Id (`trn_`), ConversationId, Model, Route, Provider, Fallback, Status (Success, Error, Cancelled), Error, PromptTokens, CompletionTokens, TimeToFirstTokenMs, TotalMs, ToolIterations, ToolCallCount, ToolMs, Surface (Web, Desktop, Terminal, Cli, Mcp), FeedbackRating, CreatedUtc. Prompt and response bodies are not stored here; the conversation holds them, and deleting a conversation deletes its content.

Retention: `ClientSettings.Chat.HistoryRetentionDays` (default 30) prunes `chat_turns` and `chat_tool_calls`; conversations are kept until the user deletes them. Settings are added to `XenoSettingsCatalog` (a new "Chat" section), so all settings editors pick them up.

### Streaming

The local API streams a turn as SSE with Wilson's named events: `turn` (ids), `thinking`, `chunk`, `tool_call` (requested, awaiting_approval, running, completed, denied), `usage`, `error`, and `done`. `XenoChatClient` parses them into typed `ChatStreamEvent` objects, so the desktop, terminal, and CLI never parse SSE themselves. A dropped stream after the first byte ends the turn with Status Error and keeps the partial answer marked as incomplete.

### MCP server

- Library: Voltaic (`McpHttpServer` and the stdio server), as used by Armada and AssistantHub.
- Transports: streamable HTTP on loopback (`ClientSettings.Mcp`, default disabled, host `127.0.0.1`, port **11436**, same loopback-only rules as the listener), and stdio through `xeno mcp`, which Claude Code can launch directly.
- Auth: the HTTP transport requires the local dashboard token, as the local API does.
- Tools, each tagged ReadOnly or Mutating:
  - ReadOnly: `xeno_status`, `xeno_list_models`, `xeno_resolve_route`, `xeno_list_providers`, `xeno_provider_health`, `xeno_request_history_query`, `xeno_chat_history_query`, `xeno_feedback_query`, `xeno_list_conversations`, `xeno_get_conversation`.
  - Mutating: `xeno_chat` (send a message to a model, optionally in a conversation), `xeno_set_route_rule`, `xeno_remove_route_rule`, `xeno_start_client`, `xeno_stop_client`, `xeno_check_providers`.
- One tool registry (`XenoToolCatalog` in `Xeno.Client.Core`) feeds both the MCP server and the in-app chat, so the two never drift.

### Tool-call loop (in-app chat)

Wilson's `ToolAgentService` loop, simplified: the catalog's tools are offered when the conversation has tools enabled and the model is known to support tool calls (a `SupportsTools` flag on catalog models, defaulting to on for OpenAI-shaped providers and tool-capable Ollama models). Limits: `MaxToolIterations` 8 and `MaxToolCallsPerTurn` 16, both settings. ReadOnly tools run immediately. Mutating tools emit `tool_call` awaiting_approval, and the UI answers through `POST /api/v1/chat/turns/{turn}/tool-calls/{id}/approval`; no answer within 120 seconds counts as Denied. A tool cannot call `xeno_chat` from inside a chat turn (no recursion). Tool calls render inline in the conversation as collapsible rows showing name, arguments, result, and duration.

### Local API (local dashboard service)

- `GET/POST /api/v1/chat/conversations`, `GET/PUT/DELETE /api/v1/chat/conversations/{id}`
- `POST /api/v1/chat/conversations/{id}/messages` (SSE turn), `POST /api/v1/chat/turns/{id}/cancel`
- `POST /api/v1/chat/turns/{turn}/tool-calls/{id}/approval`
- `PUT/DELETE /api/v1/chat/messages/{id}/feedback`, `GET /api/v1/chat/feedback` (filters: rating, model, route, has comment, from, to; paged)
- `GET /api/v1/chat/history` (filters: model, route, provider, status, surface, rating, fallback, conversation, text search on title, from, to; paged), `GET /api/v1/chat/history/{id}`, `DELETE /api/v1/chat/history` (by filter)
- `GET /api/v1/chat/analytics?from&to&bucketMinutes`: turns over time by route, latency p50/p95, time to first token, tokens, error rate, feedback up/down and negative rate, per-model breakdown.
- `GET /api/v1/chat/tools`: the tool catalog with ReadOnly or Mutating tags.

All are added to the local dashboard API Explorer, Postman, and `REST_API.md`.

### Server dashboard chat

The server gets `/v1.0/chat/...` routes mirroring the local API (minus tools), backed by `chat_conversations`, `chat_messages`, `chat_feedback`, and `chat_turns` tables in all four server database drivers (SQLite, PostgreSQL, MySQL, SQL Server), scoped by tenant and user and guarded by a new `Chat` permission. Turns go through the server's existing inference path to a client serving the model, with the requesting user's credits and ledger entries applied as for any network request. Admins get a Feedback review page and chat analytics filtered by tenant, user, and model.

### Surfaces

- **Local web dashboard**: a Chat page (conversation list, model picker grouped by local and network from the catalog, streaming messages with Markdown, thinking toggle, tool-call rows with Allow or Deny, thumbs up or down with a comment modal, timing and route chips), a Chat History page (KPI strip, charts, filterable table, turn detail with linked tool calls and feedback), and a Feedback page.
- **Desktop app**: Chat, Chat History, and Feedback pages, defined in `XenoNavigationCatalog`, with the same components drawn in Avalonia.
- **Terminal app**: the same three pages through `TuiPage.Frame`, with mouse and keyboard support, the message list rendered by `XenoMarkdown`, and approval through `ChoiceDialog`.
- **CLI**: `xeno chat` (interactive REPL with `/model`, `/new`, `/tools on|off`, `/rate up|down [comment]`, `/exit`), `xeno chat -m <model> "prompt"` (one-shot, `--json` for scripts), `xeno chat list|show|delete`, `xeno chat history [filters]`, `xeno chat feedback [filters]`, and `xeno mcp` (stdio server).
- **Tray**: "Chat..." opens the desktop Chat page through `DesktopActivation`.
- Charts come from `XenoCharts` so the web, desktop, and terminal versions match.

### Testing

Everything product-facing gets cases in `src/Test.Shared` (new `ChatTestSuites`, `McpTestSuites`, plus additions to `FrontEndTestSuites` and `TuiTestSuites`), so the automated, xUnit, and NUnit harnesses all run them:

- Storage round trips, retention, feedback replace and delete, history filters and paging, analytics math.
- Turn engine against a fake OpenAI-compatible provider and a fake tunnel: local and network routes, fallback, streaming, cancellation, a failure after the first byte, usage capture.
- Tool loop: auto-run read-only tools, approval allow, deny, and timeout, iteration and per-turn limits, no recursion.
- MCP: `initialize`, `tools/list`, and `tools/call` over HTTP and stdio; loopback-only host validation; token required.
- Front ends: page catalog entries, command availability, CLI argument parsing and one-shot output, terminal Chat page driven by mouse and keyboard through `TuiHarness`.
- Local dashboard Playwright: send, stream, rate, comment, approve a tool, filter history.
- Server: chat routes per database driver, permission checks, tenant isolation.

## Phases

1. **Foundation** (done): DTOs, local storage, `ClientSettings.Chat`, `ChatEngine` including the tool loop and approvals (driven by an `IChatToolHost`), `RoutedChatTransport`, local API with SSE, `local-api.json`, `XenoChatClient`, `ChatTestSuites`. The Chat and MCP sections are in `XenoSettingsCatalog` and the web Settings page.
2. **Local web dashboard** (done): Chat, Chat history, and Feedback pages (`dashboard/src/local/chat/`), Playwright.
3. **MCP server and tool loop** (done): `XenoToolCatalog` (15 tools), `XenoChatToolHost`, Voltaic 2.4.0 HTTP server (`XenoMcpHttpRuntime`, token in `~/.xeno/mcp-token`) and `xeno mcp` stdio, tools switch in the web chat, `McpTestSuites`. Watson moved to 7.3.2 because Voltaic 2.4.0 requires it. `xeno_check_providers` is read-only (it only probes).
4. **CLI, desktop, terminal, tray** (done): `xeno chat` (interactive, one-shot, piped, list/show/delete/rate/history/feedback/models), Chat, Chat history, and Feedback pages in the desktop and terminal apps (shared `ChatFormat`, `ChatTurnRow`, `ChatFeedbackRow`, `ChatTranscript`, `XenoCharts.ChatActivity`, `XenoWorkspace.Chat`), tray "Chat...". The chat pages have no Alt/Cmd digit (`ShortcutDigit = -1`); 0 through 9 were taken. The terminal shows messages as escaped Markdown, per AGENTS.md.
5. **Server** (done): chat models, engine, and wire types moved to `Xeno.Core/Chat` so client and server share them; `DbChatMethods` (one ADO.NET store for SQLite, PostgreSQL, MySQL, and SQL Server, tested on all four); `XenoServer.Chat.cs` with a chat entry into the network relay (request history and credits as for any network request); `ChatRoutes` under `/v1.0/api/chat/`; `Chat` permission granted to TenantMember (reconciled at startup without re-running tenant seeding); server dashboard Chat page (shared `dashboard/src/chat/ChatPage.jsx` via `ChatApiContext`) and Chat review page; Playwright and Touchstone tests.
6. **Docs and review**: README, `REST_API.md`, `DESKTOP_AND_TERMINAL.md`, `CHAT.md`, `MCP.md`, `CHANGELOG.md`, Postman, and API Explorer are done. The UI pass covered the web pages by screenshot; the desktop pages are built and use the shared presentation code but have not been looked at on screen.

## Resolved questions

Answered by the product owner on 2026-11-10.

1. Chat inference runs in the client process; the other front ends use the local API (C8).
2. `xeno_chat` calls from outside agents are saved as conversations with Surface `Mcp`, visible and rateable everywhere and filterable out.
3. The HTTP MCP server is opt-in on loopback port 11436. `xeno mcp` (stdio) always works.
4. Server-dashboard chat spends credits and writes ledger entries like any other network request.

# Changelog

## Unreleased

- Added server-requested client telemetry settings, WebSocket frame contracts, SQLite-backed server telemetry persistence, authenticated REST telemetry routes, dashboard `CONTROL > Client Telemetry`, telemetry route wrappers in C#, JavaScript, and Python SDKs, and telemetry routing policy fields.

## 0.1.0

- Added initial Xeno server, client, dashboard, Docker, documentation, and Touchstone test surfaces.
- Added multitenant seeded state and OpenAI-compatible routing.
- Added WebSocket client registration endpoint.
- Added C#, JavaScript, and Python SDKs with tests, documentation, and interactive console harnesses.
- Added `xeno reset` and `xeno reset --force` to restore client settings to factory defaults.
- Added `xeno model delete` to remove advertised models from client configuration.
- Added background client process support: `xeno connect` and `xeno start` now return after registration, with `xeno connect --foreground` for attached operation.
- Added `xeno logs`, `xeno logs -f`, `xeno stop`, and `xeno disconnect` for background client operation.
- Added `ls`, `rm`, `remove`, and `remote` aliases for supported provider and model commands.
- Added model capability flags to `xeno model add` through `--type completion|embedding|both`, `--completions`, and `--embeddings`.
- Updated client configuration to write provider endpoint API family as `ApiType`; legacy `Provider` fields are still read for compatibility.
- Updated `xeno config show` to display provider endpoint models and capabilities in formatted output.
- Added default-enabled Xeno client local dashboard hosting from the background process with local SQLite request history, provider/model management UI and APIs, request summaries, client logs, settings editing, and `xeno dashboard` / `xeno dashboard open` discovery commands.
- Added `CLIENT_TELEMETRY.md` as the implementation plan for RigMonitor-style CPU, memory, disk, and GPU telemetry in Xeno Client and the local dashboard.
- Updated markdown documentation and Postman coverage for current Xeno Client commands, local dashboard behavior, routing policies, request history, and SDK scope.
- Added Xeno Client local CPU, memory, disk, and GPU telemetry collection with SQLite persistence, retention pruning, local dashboard Telemetry and Telemetry Analytics pages, local telemetry API routes, settings controls, and Touchstone coverage for telemetry defaults and clamps.
- Added Xeno Client provider runtime telemetry planning and implementation surfaces for Ollama, vLLM, OpenAI-compatible endpoints, local dashboard Provider Telemetry pages, SQLite persistence, local API routes, request-history correlation, Postman coverage, and Touchstone tests.
- Improved server and client dashboard chart density, axis label limits, tooltip coverage, action menu viewport positioning, and local telemetry/request-history layout.
- Fixed API Explorer non-streamed response timing so Time To First Byte reflects response header arrival instead of showing zero/null.

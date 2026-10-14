# Changelog

## Unreleased

- Added the Xeno desktop app (`xeno-desktop`, Avalonia) with Overview, Models, Providers, Requests, Logs, Host and Provider (telemetry), General, and Settings pages, a command palette, light and dark appearance, and a native macOS menu bar named "Xeno" (File, Edit, View, Client, Window, Help) with an in-window menu on Windows and Linux.
- Added the Xeno terminal app (`xeno tui`, TUIKit) with the same pages, menus, forms, and settings as the desktop app, plus a menu bar (F10), command palette (Ctrl+K), key help (F1), Alt+digit page navigation, and single-key page actions.
- Reworked the tray app into the Xeno Agent: it now launches the desktop app, the terminal app (in a new terminal window), and the local web dashboard, starts, stops, and restarts the client in-process, has its own macOS application name and menu, and appears in the Dock only while a window is open.
- Added a unified model catalog that shows, for every model, whether this machine serves it, whether the Xeno network offers it, or both, using the server's `/v1/models` list with the client's access key.
- Added `src/Xeno.Client.Core`, a shared client library used by the CLI, local dashboard, desktop app, terminal app, and tray agent: settings, state, the local history database, telemetry, provider and model configuration, worker start and stop, server sign-in and ping, log reading, settings validation, executable discovery, terminal launching, and the shared navigation, menus, forms, and settings catalog. The CLI and local dashboard now call these shared services instead of their own copies, with unchanged command output and API behavior.
- Added `DESKTOP_AND_TERMINAL.md` and desktop, terminal, and tray sections to the getting-started guides, and Touchstone `SharedClient`, `Tray`, `Desktop`, and headless `Tui` suites.
- Added `scripts/` with `macos/`, `linux/`, and `windows/` folders:
  - Run from source: `run-desktop`, `run-tui`, `run-tray`, `run-cli`, `run-client`, `run-server`, `run-dashboard`, and `run-local`. `run-local` runs a local server, the web dashboard, and a client app together in an isolated client home.
  - Manage installs: `install`, `reinstall`, `update`, `remove`, and `factory-reset`. Installs include `Xeno.app` on macOS, built by `macos/build-app.sh`.
  - `build` and `test`.
  - Local servers run from `artifacts/local-server`, so they no longer change the tracked `xeno.json`.
- The desktop app and terminal app can point at any central Xeno server, and the Xeno cloud stays the default:
  - `--server <cloud|url|host[:port]>` and `--home <dir>` on `xeno-desktop` and `xeno tui`.
  - `xeno server use` and `xeno server show`.
  - The server in use is shown in the desktop status card and the terminal app header.
  - When the desktop or terminal app opens the terminal app or the tray agent, it passes on the client home directory.
  - The installed app folders on Windows and Linux are added to the app search path.
- Restyled the desktop app, the tray agent's windows, and the terminal app in the Xeno logo colors (green `#42dc7d` and `#21964e`, purple `#4a2054`). The desktop app uses a purple sidebar with a green highlight on the current page. The terminal app gets its own light and dark themes (`XenoTuiTheme`), with a purple header, selections, and menu bar and green accents and focus. The brand colors are defined once in `XenoBrand`.
- Fixed sign-in and connection to the Xeno cloud:
  - The client default is now `https://app.xenocloud.ai:9000` (WebSocket `wss://app.xenocloud.ai:9000/ws/v1/clients`). The old `ws://` and `http://` default was rejected by the cloud ("The plain HTTP request was sent to HTTPS port").
  - Older `client.json` files with the plain-HTTP cloud address are upgraded when loaded.
  - A server that requires HTTPS now produces a clear error that suggests the `https://` address.
- Fixed the NU1902/NU1903 build warnings by pinning `System.Security.Cryptography.Pkcs` to 10.0.13 in `Xeno.Core`. This fixes GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7 in the 9.0.18 version that `Microsoft.Data.SqlClient` 7.1.1 brings in.
- Charts in the desktop and terminal apps now match the local web dashboard. They are built once in `XenoCharts` (`Xeno.Client.Core`):
  - Requests over time on Overview and Requests, with a range picker on both pages.
  - CPU, memory, and GPU charts on Host Telemetry, replacing the single metric picker.
  - The chosen metric on Provider Telemetry.
  - Every chart has a heading with its range, a legend, its peak, time labels, and, in the desktop app, a tooltip per column.
  - An empty chart says why (for example, "No requests in the last hour") instead of drawing a bare `0`.
- The terminal app reports what it is doing and what it did:
  - A message line above the key hints shows a spinner while work runs, then the outcome with the time and a ✓, !, or ✗ marker. Errors also raise a notice.
  - Signing in, in both apps, shows "Signing in...", then a confirmation that names the server and tenant and offers Start Client (or Restart Client).
- The terminal app keeps one empty column between every pane border and its content (text, tables, charts, buttons, and fields), and needs a terminal of at least 120x36. Smaller windows show TUIKit's "Terminal too small" screen.
- The desktop and terminal Logs pages roll up runs of `server.telemetry.request` and `server.telemetry.ack` messages into one line each, for example `Server message: server.telemetry.ack (5 since 11:16:39)`.
- The terminal app uses TUIKit 1.6.0 and renders its detail views (request details, provider models, About) as Markdown with TUIKit's width-aware renderer: bordered tables sized from their content, styled headings, and code blocks for headers and bodies. `XenoMarkdown` and `RequestDetailFormatter.ToMarkdown` (`Xeno.Client.Core`) build the Markdown and escape or fence every value, so request and response content renders verbatim.
- Renamed pages:
  - In the sidebar, Host Telemetry and Provider Telemetry are now **Host** and **Provider** under the Telemetry heading. Menus and page headers keep the full names.
  - Server & Account is now **General**.
- Fixed macOS memory telemetry: it recorded only total memory, so memory utilization was always empty. It now counts active, wired, and compressed pages from `vm_stat`.
- Fixed signing in to a different server: the desktop and terminal Sign In form and `xeno login --server` changed only the REST URL, so the worker kept connecting its WebSocket to the old server. They now point the whole connection at the new server.

## 0.1.1

- Updated NuGet dependencies: Caching 5.1.2, Microsoft.Data.SqlClient 7.1.1, SyslogLogging 2.3.1, Watson 7.2.2, SendWithMailgun 1.2.0, Touchstone 0.2.0 (Core, Cli, Xunit and NUnit adapters), NUnit 5.0.0, and coverlet.collector 10.1.0.
- Added Touchstone coverage for the `Caching`-backed `EntityCache` (read-through hits, invalidate/clear reloads, least-recently-used eviction, argument validation, and null-load handling).
- Added `build-server.sh`, `build-dashboard.sh`, `build-all.sh`, and `build-local-dashboard.sh` for macOS/Linux; the Docker scripts build and push multi-architecture images tagged with the requested tag and `latest`.
- Bumped the application, protocol, and assembly versions to 0.1.1. Docker images continue to publish under the `v0.1.0` tag.
- Updated NuGet dependencies (Watson 7.2.0, SyslogLogging 2.2.2, Microsoft.Data.Sqlite 10.0.12, Microsoft.Data.SqlClient 7.1.0, OpenTelemetry 1.19.x, Avalonia 12.1.3, and test tooling).
- Fixed a race where a streamed relay whose start, chunks, and completion arrived back to back could be handled as a buffered response and lose its `ClientResponseStart` timeline stage; added burst-streaming and pending-response ordering, final-only, and cancellation Touchstone tests.
- Added cross-tenant shared clients: a client owned by one tenant can be made an eligible routing target for other tenants. Sharing is a per-client `Shared` flag (default off), server-authoritative and set only by the owner tenant through `POST /api/v1/clients/{id}/sharing`; the connecting node never declares its own sharing and the value is preserved across reconnects. New tenant settings `DefaultClientShared` (seeds a client's initial shared value at first registration) and `ConsumeShared` (consumer off switch, default on) govern participation. A foreign shared client is eligible only when `Shared && Active` and the consuming tenant's `ConsumeShared` is true. `GET /api/v1/clients` returns a redacted `ClientView` projection for foreign shared clients that omits owner identity, tenant id, weight, in-flight, and max-parallel and reduces models to name plus capabilities, with an `origin` field of `tenant`, `operator`, or `third-party`; `GET /api/v1/admin/shared-clients` lists all shared clients across tenants for system admins; `GET /v1/models` marks shared-origin models `owned_by: "shared"` and `/api/tags` includes shared models; telemetry status reports `sharedEligibleClientCount`. Capacity across a shared client is first-come-first-served in v1 (owner requests are not prioritized). Added `SetClientSharingAsync`/`setClientSharing`/`set_client_sharing` to the C#, JavaScript, and Python SDKs and documented the feature across the routing, security, REST, and WebSocket references.
- Added full OpenTelemetry observability for the Xeno server: metrics, distributed traces, and logs exported over OTLP to an OpenTelemetry Collector, fanned out to Prometheus, Tempo, and Loki, and visualized in Grafana. Instrumentation covers HTTP requests, LLM relay (time to first token, streaming duration, tokens, round-trip and upstream timing, payload sizes), client connections/registrations/evictions, auth and RBAC mutations, email, ledger events, fleet host/provider telemetry, and .NET runtime/process metrics. Ships a pre-provisioned Docker stack (Collector, Prometheus, Tempo, Loki, Grafana on port 9400 with anonymous access), eight pre-built Grafana dashboards, a `Telemetry` settings block with environment overrides, a Grafana link card on the dashboard Settings page, an `OBSERVABILITY.md` guide, and a positive/negative Touchstone `Observability` test suite. The legacy `/metrics` endpoint is retained and now shares the same recording call sites.
- Added an optional Xeno Client local inference listener with loopback-only OpenAI/Ollama-compatible APIs, websocket proxying, streaming support, dashboard explorer, Postman coverage, documentation, and Touchstone tests.
- Changed server settings GET/POST responses to return the `XenoSettings` JSON object directly, matching the POST request body shape.
- Added NetLedger-backed token ledgering with tenant lifecycle cascade, metadata-based duplicate detection, dashboard summaries, SDK/CLI/Postman coverage, Docker wiring, and raw-token balance/entry APIs.
- Added global-admin administrative test email APIs, dashboard controls, SDK wrappers, Postman coverage, and REST documentation for validating configured email providers with the standard Xeno email layout.
- Added provider-abstracted server email plumbing, disabled/file/Mailgun providers, onboarding notification templates, email settings, dashboard configuration, metrics, and Touchstone coverage.
- Hardened RBAC and tenant fencing: system-admin and tenant-admin flags are protected, role/permission grants enforce grantor ceilings, OpenAI-compatible routes require authenticated inference permissions, request history includes caller identity, and SDKs/docs/Postman now describe authenticated OpenAI usage.
- Added server-requested client telemetry settings, WebSocket frame contracts, SQLite-backed server telemetry persistence, authenticated REST telemetry routes, dashboard `CONTROL > Client Telemetry`, telemetry route wrappers in C#, JavaScript, and Python SDKs, and telemetry routing policy fields.
- Expanded documentation around Xeno's broader platform vision: a controller-agent model for federating contributor GPUs, private data center accelerators, hybrid provider capacity, and agentic application access through one OpenAI-compatible control plane.

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

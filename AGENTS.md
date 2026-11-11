# AGENTS.md

Follow `C:\code\agents\requirements` when editing this repository.

## Build

```powershell
dotnet build src/Xeno.sln
dotnet run --project src/Test.Automated -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

## Style

- No `var`.
- No tuple returns.
- `using` statements belong inside namespace blocks.
- Public APIs need XML documentation.
- Async methods that perform cancellable work accept `CancellationToken`.
- Use `ConfigureAwait(false)` in library/server code.

## Testing

Shared test descriptors live in `src/Test.Shared` and run through Touchstone:

- `src/Test.Automated`
- `src/Test.Xunit`
- `src/Test.Nunit`

## Local Inference Server

- The Xeno Client local inference listener is configured by `ClientSettings.LocalInferenceServer` in `~/.xeno/client.json`.
- Defaults are disabled, loopback host `127.0.0.1`, port `11435` (Ollama keeps `11434`), CORS enabled for local app integration, and `RoutingMode` `PreferLocal`. A stored port of exactly `11434` is migrated to `11435` once and surfaced as a one-time notice (`ClientConnectionState.LocalInferencePortMigratedFrom`).
- Providers carry `ShareWithNetwork`, `RouteLocalRequests`, and nullable `MaxConcurrentRequests`; models carry nullable `MaxConcurrentRequests`; `ClientSettings.DefaultMaxConcurrentRequests` (default 4) applies where a provider sets none. Resolve limits only through `ConcurrencyLimitResolver`. Local-only providers (`ShareWithNetwork = false`) must never be announced to the server or serve network requests.
- Loading existing client settings must backfill missing `LocalInferenceServer` defaults and rewrite `client.json`.
- The listener is implemented under `src/Xeno.Client/LocalInference` as a Watson7 service and runtime separate from the local dashboard.
- The listener must remain loopback-only. Accept `localhost`, `127.0.0.1`, `::1`, and `[::1]`; reject wildcard, public, and non-local DNS hosts.
- Supported local APIs are only the OpenAI-compatible `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`, `/v1/models`, `/v1/models/{model}` routes and the Ollama-compatible `/api/generate`, `/api/chat`, `/api/embeddings`, `/api/embed`, `/api/tags`, `/api/show`, `/api/ps` routes.
- Every model request is resolved by `LocalInferenceRouter` (`src/Xeno.Client.Core/Routing`). `PreferLocal` sends a model served by a routable local provider (configured, or discovered when `DiscoverLocalModels` is on) straight to that provider through `LocalProviderProxy`; everything else goes through typed websocket messages in `src/Xeno.Core/Models`. Rules in `LocalInferenceServer.Routes` override the default per model. Front ends must ask the router (or the catalog's `RouteTarget`) rather than reimplementing the decision.
- Local passthrough forwards request bodies as bytes without parsing beyond the request DTO the route already reads; it preserves cancellation, streaming chunks, status codes, and content types, drops hop-by-hop, `Set-Cookie`, `Access-Control-*`, and `X-Xeno-*` provider headers, and replaces the caller's `Authorization` with the provider credential.
- Every listener response carries `X-Xeno-Route` (`local:<provider>`, `network`, `local`, `local+network`, or `unavailable`). `X-Xeno-Fallback: true` marks a network fallback.
- Network fallback happens only when the model's route rule sets `FallbackToNetwork`, only before the first response byte, and always sets `ExcludeRequestingClient` on the tunnel request. A failure after the first byte ends the response with an error in the caller's shape (an SSE error event or a final NDJSON error object).
- Errors are per backend. A down local provider returns a provider-shaped 502 for its models only; when the websocket tunnel is unavailable, disconnected, severed, or unauthorized, network-routed requests return provider-shaped 502 responses while local models keep working.
- `/v1/models`, `/api/tags`, and `/api/show` are built by the client: local models first, then network models this machine does not serve. `/api/ps` lists local Ollama providers only. A listing returns 502 only when no local provider and no tunnel answered. `NetworkOnly` keeps the pre-0.2.0 passthrough behavior for every route.
- Ollama-shaped requests for a model served by an OpenAI-only provider (vLLM, OpenAI-compatible) return a provider-shaped 400 pointing at the `/v1` routes until shape translation lands.
- Use named DTO classes for local inference requests and responses. Do not inspect request payloads with `JsonNode`, `JsonArray`, `JsonElement`, `JsonObject`, or JSON string contains checks; `System.Text.Json` serialization to and from named types is allowed.
- Prompt, chat, and embedding input content must not be logged unless an explicit existing diagnostic setting permits it.
- Network and local requests to the same provider share `ProviderConcurrencyGate.Shared`; never call a provider for a routed request without a lease. Heartbeats report `networkCapacity` (shared provider limits minus local in-flight), and a full provider answers network requests with `ClientResponseMessage.Busy`, which the server retries once on another client.
- The running listener publishes provider health and counters to `LocalInferenceRuntimeStatus` and metadata-only request records to `LocalInferenceRequestLog`; other processes (CLI, desktop, terminal, tray, local dashboard) read those through `LocalInferenceRoutingService` and `XenoWorkspace`, never by talking to the listener.
- The Local endpoint page (`XenoPageEnum.LocalEndpoint`) exists in the local web dashboard, the desktop app, and the terminal app, and the tray opens it. Keep them in step with `LocalInferenceRoutingView`.
- Keep `xeno status`, `xeno inference`, client state, local dashboard settings, the Local endpoint page, the local dashboard API Explorer, Postman, and markdown documentation in sync with local inference behavior.
- Cover local inference changes in `src/Test.Shared` whenever behavior is product-facing so the automated, Xunit, and NUnit harnesses all exercise it.

## Client Front Ends

- The client has five front ends: the `xeno` CLI, the local web dashboard, the desktop app (`src/Xeno.Desktop`), the terminal app (`src/Xeno.Tui`, hosted as `xeno tui`), and the tray agent (`src/Xeno.Tray`). Keep them consistent; see `DESKTOP_AND_TERMINAL.md`.
- Put client behavior in `src/Xeno.Client.Core`, not in a front end. The desktop and terminal apps go through `XenoWorkspace`; the CLI and local dashboard call the same services (`ProviderConfiguration`, `ClientWorkerController`, `XenoServerClient`, `ClientLogReader`, `LocalSettingsEditor`).
- Pages, menus, command enablement, forms, and editable settings are defined once in `Xeno.Client.Presentation` (`XenoNavigationCatalog`, `XenoMenuLayout`, `XenoCommandAvailability`, `XenoForms`, `XenoSettingsCatalog`). Add a page, command, form field, or setting there, then render it in both the desktop and terminal apps.
- Shared Avalonia code (theme, native menus, dialogs, tables, About window) lives in `src/Xeno.Desktop.Common` and is used by both the desktop app and the tray agent. UI is built in code; Avalonia XAML compilation is disabled.
- On macOS the desktop app must be named "Xeno" and the tray agent "Xeno Agent" (`Application.Name`), each installing its application menu in `Initialize()` before Avalonia adds its default menu.
- The model catalog (`ModelCatalogBuilder`) merges models served by this machine with models the Xeno network offers. Front ends show models from the catalog rather than reading providers directly.
- Client apps point at the Xeno cloud by default (`ServerTargets.CloudWebSocketUrl`). Server selection (`--server`, `xeno server use`, sign-in) goes through `ServerTargets` and `XenoLaunchOptions` in `Xeno.Client.Core`, so every front end accepts the same values (`cloud`, http(s), ws(s), or host[:port]). Keep the README and `DESKTOP_AND_TERMINAL.md` ("Choosing the Xeno server") in sync.
- `scripts/` holds per-OS run, install, update, remove, and reset scripts (`scripts/common/*.sh` with `macos/` and `linux/` wrappers, and `windows/*.bat`). When you add an app or change how one starts, update all three platforms and `scripts/README.md`. Local servers run from `artifacts/local-server`; never let scripts modify the tracked `xeno.json`.
- Charts come from `XenoCharts` (`Xeno.Client.Core/Presentation`) and match the local web dashboard; the desktop `ColumnChart` and the terminal `ChartView` only draw a `XenoChart`. Add or change a chart there, then in the web dashboard.
- Desktop and tray colors come from `XenoColorSchemes` (`Xeno.Client.Core/Presentation`) through `XenoTheme` and `XenoPalette`. Never hardcode a color in a page; use the palette, and add a new palette to the catalog, where `ColorSchemeTestSuites` checks its contrast.
- Front ends connect through `XenoAutoConnect` when they start and bring up the tray with `XenoAppLauncher.EnsureTrayRunning` (never from the background worker). Tests run with `XENO_AUTOCONNECT=0` and `XENO_AGENT_AUTOSTART=0` set by `Test.Shared/TestEnvironment.cs`.
- Usage charts come from `UsageAnalyticsBuilder` (`Xeno.Core/Analytics`) and `XenoChartCatalog` (`Xeno.Client.Core/Presentation`). Add a chart in both before any surface draws it, and regenerate `dashboard/src/charts/chartCatalog.json`. Surfaces never compute percentiles or buckets. See `USAGE_ANALYTICS.md`.
- Terminal app panes go through `TuiPage.Frame`, which wraps content in `Inset`/`FocusInset` (one empty column inside each border). Report progress with `TuiContext.Progress` and outcomes with `TuiContext.Notify`; both show on the message line. The app's minimum size is `XenoTuiApp.MinimumWidth` x `MinimumHeight` (120x36).
- Every terminal app control must work with the mouse as well as the keyboard (`TuiTestSuites` Mouse cases drive clicks through `TuiHarness.Click`). Overlays that take clicks must be modals: the open menu bar is `MenuBarModal`, page dropdowns are wrapped by `TuiPage.Frame` in `DropdownPicker` (choices open as a `ContextMenu` popup), and confirmations use `ChoiceDialog`, not TUIKit's `MessageModal`, which ignores the mouse.
- Long text in the terminal app is Markdown shown with `TuiContext.ShowMarkdownAsync` (`MarkdownViewerModal`, TUIKit's width-aware renderer). Build it in `Xeno.Client.Core` with `XenoMarkdown` so values are escaped and bodies are fenced; never pass raw request, response, or log content as Markdown.
- Cover front-end changes in `src/Test.Shared` (`FrontEndTestSuites`, `TuiTestSuites`). Terminal app tests run headlessly through `TuiHarness`; never point tests at the real `~/.xeno` (use `ClientHomeScope`).

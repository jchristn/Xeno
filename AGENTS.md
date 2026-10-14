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
- Defaults are disabled, loopback host `127.0.0.1`, port `11434`, and CORS enabled for local app integration.
- Loading existing client settings must backfill missing `LocalInferenceServer` defaults and rewrite `client.json`.
- The listener is implemented under `src/Xeno.Client/LocalInference` as a Watson7 service and runtime separate from the local dashboard.
- The listener must remain loopback-only. Accept `localhost`, `127.0.0.1`, `::1`, and `[::1]`; reject wildcard, public, and non-local DNS hosts.
- Supported local APIs are only the OpenAI-compatible `/v1/chat/completions`, `/v1/embeddings`, `/v1/models`, `/v1/models/{model}` routes and the Ollama-compatible `/api/generate`, `/api/chat`, `/api/embeddings`, `/api/embed`, `/api/tags`, `/api/show`, `/api/ps` routes.
- Local requests are proxied through typed websocket messages in `src/Xeno.Core/Models`; preserve cancellation, streaming chunks, status codes, content types, and safe response headers from the Xeno server.
- When the websocket tunnel is unavailable, disconnected, severed, or unauthorized, local endpoints return provider-shaped 502 responses.
- Use named DTO classes for local inference requests and responses. Do not inspect request payloads with `JsonNode`, `JsonArray`, `JsonElement`, `JsonObject`, or JSON string contains checks; `System.Text.Json` serialization to and from named types is allowed.
- Prompt, chat, and embedding input content must not be logged unless an explicit existing diagnostic setting permits it.
- Keep `xeno status`, client state, local dashboard settings, the local dashboard API Explorer, Postman, and markdown documentation in sync with local inference behavior.
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
- Terminal app panes go through `TuiPage.Frame`, which wraps content in `Inset`/`FocusInset` (one empty column inside each border). Report progress with `TuiContext.Progress` and outcomes with `TuiContext.Notify`; both show on the message line. The app's minimum size is `XenoTuiApp.MinimumWidth` x `MinimumHeight` (120x36).
- Long text in the terminal app is Markdown shown with `TuiContext.ShowMarkdownAsync` (`MarkdownViewerModal`, TUIKit's width-aware renderer). Build it in `Xeno.Client.Core` with `XenoMarkdown` so values are escaped and bodies are fenced; never pass raw request, response, or log content as Markdown.
- Cover front-end changes in `src/Test.Shared` (`FrontEndTestSuites`, `TuiTestSuites`). Terminal app tests run headlessly through `TuiHarness`; never point tests at the real `~/.xeno` (use `ClientHomeScope`).

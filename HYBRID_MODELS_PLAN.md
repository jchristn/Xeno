# Hybrid Models Plan

Status: Draft, not started
Owner: unassigned
Last updated: 2026-11-09

## Summary

The Xeno client's local inference listener is a straight pipe today. Every request that reaches `/v1/*` or `/api/*` on the loopback port is wrapped in a `ClientLocalInferenceRequestMessage`, sent over the websocket tunnel, and answered by the Xeno server, including model listings. A user who runs Ollama on the same machine has two endpoints to juggle, and if that machine also contributes its Ollama to the network, a local request for a local model takes a round trip through the cloud to come back to the same box.

The listener becomes a router. For each request it resolves the model to a target: a provider configured on this machine (Ollama, vLLM, or any OpenAI-compatible endpoint, including Ollama's own cloud API) or the Xeno network. Local wins by default. Network fallback is opt-in per model. Listings merge both sides. Local traffic shares concurrency limits with the network work the machine serves. The listener moves off port 11434 so it stops fighting Ollama for it.

The second half of the plan makes usage visible. Xeno records a lot (request history with token counts and timings, host and provider telemetry, ledger credits and debits) and shows very little of it: request counts in a stacked bar, a token leaderboard, and a ledger trend. Every aggregation happens in memory over raw rows, and prompt and completion tokens sit inside a JSON blob. The usage phases add structured rollups on the server and the client, one usage API shape shared by both, and a usage experience modeled on Mux and Armada across every surface: the server dashboard, the local dashboard, the desktop app, the terminal app, the tray agent, the CLI, and the SDKs. The plan closes with a UI, aesthetics, and UX examination of everything it built.

The decisions below were made with the product owner on 2026-11-09 and are not open for relitigation inside this plan. Answers to the questions raised while planning are under "Resolved questions".

## Decisions

| # | Decision |
|---|---|
| D1 | Prefer local by default. A model served by an enabled, routable local provider is sent there directly. |
| D2 | The listener moves to a new default port, **11435**. Ollama keeps 11434. Existing settings that still hold the old default are migrated. |
| D3 | The listener accepts both the Ollama and OpenAI API shapes for every target, translating where the backend does not speak the caller's shape. |
| D4 | `/v1/models`, `/api/tags`, and `/api/show` merge local and network models. `/api/ps` reports local backends only. |
| D5 | Fallback to the network is opt-in per model and never happens after the first response byte. |
| D6 | Direct local requests count against the same per-provider concurrency limits as network requests this machine serves. |
| D7 | Errors are per backend and provider-shaped. A dead local backend does not break network-routed models, and a dead tunnel does not break locally routed ones. |
| D8 | The route table, backend health, and per-request route are visible in `xeno status`, the local web dashboard (including the API Explorer), the desktop app, the terminal app, and the tray agent. |
| D9 | SharpAI is out of scope for this plan. See "Later". |
| D10 | Usage visualization follows the patterns in Mux (`~/Code/Mux`) and Armada (`~/Code/Armada`), on the server and the client, in every user-facing surface. |
| D11 | The final phase is a UI, aesthetics, and UX examination of everything the plan builds: chart controls, timeframe selectors, auto-refresh, color, charts, heatmaps, and their supporting tables. |

## Correction to an assumption

The brainstorm assumed Xeno already converts between Ollama and OpenAI shapes. It does not. Nothing in the client, server, or core translates request bodies, responses, or stream chunks. The worker (`Program.ExecuteServerRequestAsync`, `src/Xeno.Client/Program.cs:3003`) forwards bodies unchanged to `runner.Endpoint + relativePath`, and the server only matches the `ollama` provider family to Ollama providers (`XenoServer.cs:4071`).

The good news is that most of the matrix is still passthrough. Ollama serves the OpenAI routes natively, so an OpenAI-shaped request to a local Ollama needs no conversion. The real work is Ollama-shaped requests to OpenAI-only backends (vLLM, OpenAI-compatible endpoints), which need typed translators for bodies and for NDJSON to SSE streaming. Phase 4 owns that work, and Phases 1 to 3 ship without it by returning a clear provider-shaped error for that one combination.

| Caller shape | Ollama backend | vLLM / OpenAI-compatible backend | Xeno network |
|---|---|---|---|
| OpenAI (`/v1/*`) | Passthrough | Passthrough | Tunnel (today) |
| Ollama (`/api/*`) | Passthrough | Translate (Phase 4) | Tunnel (today) |

## How a request is routed

Resolution happens once per request in a new `LocalInferenceRouter` in `Xeno.Client.Core`, so the CLI, dashboard, desktop, and terminal apps can all ask the same question ("where would model X go?") through one code path.

1. Normalize the model name. Ollama treats `llama3.1` and `llama3.1:latest` as the same model, and the router must too.
2. If a route rule exists for the model, use its target: a named local provider, or the network.
3. Otherwise, if `RoutingMode` is `PreferLocal` and one or more routable local providers serve the model, pick the first healthy one in provider order.
4. Otherwise send it to the network.
5. If the chosen local target is unhealthy, refuses the connection, or times out before sending a byte, and the rule has `FallbackToNetwork` set, send the request to the network with `ExcludeRequestingClient` set. Otherwise return a provider-shaped 502.

`ExcludeRequestingClient` matters more than it looks. Without it, a fallback for a model this machine also serves to the network can be routed by the server right back to this client, into the same dead backend.

`RoutingMode` has three values. `PreferLocal` is the default. `NetworkOnly` reproduces today's behavior exactly and doubles as the escape hatch if routing misbehaves in the field. `LocalOnly` never touches the tunnel, which suits a user who wants a single endpoint for their own models and nothing else.

Local models come from two places: the models configured on each provider (`ClientModelSettings`) and the models the backend actually reports (`/api/tags` or `/v1/models`, using the DTOs `ClientProviderTelemetryService` already has). Discovery matters for the headline scenario. A signed-in local Ollama lists its Ollama Cloud models (the `-cloud` tags) next to its local ones, and the user expects to reach both without typing each name into Xeno.

## Who sees which provider

Today every configured provider is a provider this machine serves to the network. Routing makes that assumption wrong. A user with an Ollama Cloud subscription, or an `https://ollama.com` endpoint configured with their own API key, almost certainly does not want to resell that capacity to the Xeno network, and the provider's terms may forbid it.

Each provider therefore gets two flags. `ShareWithNetwork` (default `true`, so existing configurations behave as they do today) controls whether the worker offers its models to the network. `RouteLocalRequests` (default `true`) controls whether the listener may route to it. A local-only provider is `ShareWithNetwork = false`. The catalog, the model announcements the worker sends, and every front end must respect the split.

## Settings shape

All new settings live in `~/.xeno/client.json`, are backfilled by `ClientSettings.ApplyDefaults`, and are clamped and validated on load. Nothing here is persisted on the Xeno server.

| Setting | Type | Default | Range / values | Live or restart |
|---|---|---|---|---|
| `LocalInferenceServer.Port` | int | 11435 | 1 to 65535 | Restart listener |
| `LocalInferenceServer.RoutingMode` | enum | `PreferLocal` | `PreferLocal`, `NetworkOnly`, `LocalOnly` | Live |
| `LocalInferenceServer.Routes` | list of `LocalInferenceRouteRule` | empty | see below | Live |
| `LocalInferenceServer.DiscoverLocalModels` | bool | true | | Live |
| `LocalInferenceServer.HealthCheckIntervalMs` | int | 15000 | 1000 to 300000 | Live |
| `LocalInferenceServer.LocalQueueTimeoutMs` | int | 30000 | 0 to 300000 (0 = fail immediately when full) | Live |
| `Providers[].ShareWithNetwork` | bool | true | | Live |
| `Providers[].RouteLocalRequests` | bool | true | | Live |
| `DefaultMaxConcurrentRequests` | int | 4 | 1 to 256 | Live |
| `Providers[].MaxConcurrentRequests` | int? | null (inherit global) | 1 to 256 | Live |
| `Providers[].Models[].MaxConcurrentRequests` | int? | null (inherit provider) | 1 to 256 | Live |

`LocalInferenceRouteRule` has `Model` (string, required, normalized), `Target` (`Local` or `Network`), `Provider` (string, required when `Target` is `Local`), and `FallbackToNetwork` (bool, default `false`). Rules match exact model names only. Glob matching is listed under "Later".

Concurrency limits resolve from the most specific level. A model's limit applies to that model on that provider. The provider's limit caps the provider's total across all of its models. The global `DefaultMaxConcurrentRequests` applies wherever a provider sets no limit. A request needs a free slot at both the model level and the provider level. Validation warns (but allows) a model limit above its provider limit, since the provider limit wins anyway.

Port migration follows the precedent `ApplyDefaults` already set for `RequestTimeoutMs` (120000 to 300000). A stored `Port` of exactly 11434 is rewritten to 11435 once, the rewrite is recorded so `xeno status` and the front ends can tell the user once, and any other stored port is left alone.

## Phases

Each phase ships on its own and leaves the product consistent across every front end. Do not start a phase until the one before it meets its exit criteria.

| Phase | Name | Ships |
|---|---|---|
| 0 | Branch and version | `feature/v0.2.0`, every version and image tag at 0.2.0 (alpha) |
| 1 | Port and settings foundation | New default port and migration, new settings with backfill, provider flags, no routing yet |
| 2 | Direct local routing | Router, passthrough proxy to local providers, fallback, merged listings, health, route header |
| 3 | Shared concurrency and visibility | Concurrency gate, local request history, every front end, docs, Postman |
| 4 | Shape translation | Ollama-shaped requests to OpenAI-only backends, including streaming |
| 5 | Usage data foundation | Structured usage columns, rollups, utilization samples, one usage API on server and client, shared chart model and palette |
| 6 | Usage visualization | Usage pages and controls in the server dashboard, local dashboard, desktop, terminal, tray, CLI, and SDKs |
| 7 | Release readiness | Simulated user testing, cross-front-end consistency pass |
| 8 | UI, aesthetics, and UX examination | Final review and polish of everything built, then CHANGELOG and sign-off |

Phase 4 and Phase 5 do not depend on each other and can run in parallel. Phase 6 needs Phases 3 and 5. Phase 8 is last, always.

### Tracking conventions

Mark each item in place. `[ ]` not started, `[~]` in progress, `[x]` done, `[-]` dropped (add a one-line reason). Append the initials of whoever is working on it and the date to the end of the line when you change its state, for example `[~] HM-2.4 ... (JC 2026-11-12)`. Record anything surprising in the progress log at the bottom rather than rewriting the item.

## Phase 0: Branch and version

All work happens on `feature/v0.2.0`. The version moves to 0.2.0 at the start so every doc, image tag, and asset written during the plan already says 0.2.0. Phase 8 sweeps for stragglers at the end. 0.2.0 is still alpha, but the Docker image tag is exactly `v0.2.0`. The product owner explicitly requested this bump on 2026-11-09.

- [x] HM-0.1 Create and enter `feature/v0.2.0`. (2026-11-09)
- [x] HM-0.2 Bump `<Version>` in every `src/**/*.csproj`, `Constants.Version`, the client version constants in `Program.cs` and `ClientSettings.cs`, and the listener OpenAPI version to 0.2.0. (2026-11-09)
- [x] HM-0.3 Bump `dashboard/package.json` and its lockfile root, `src/sdk/javascript/package.json`, and `src/sdk/python/pyproject.toml` to 0.2.0. (2026-11-09)
- [x] HM-0.4 Docker image tags to `v0.2.0`: `docker/compose.yaml`, `docker/compose.cluster.yaml`, `DOCKERHUB_README.md`, `README.md`, the `build-*.sh` usage examples. (2026-11-09)
- [x] HM-0.5 Version text in `README.md`, `MODEL_ROUTING.md`, `WEBSOCKETS_API.md` (protocol version), the SDK READMEs, the dashboard alpha badges and i18n strings, and the SDK test fixtures. (2026-11-09)
- [x] HM-0.6 `CHANGELOG.md`: the `Unreleased` section becomes `0.2.0 (alpha, in progress)` with the bump recorded. (2026-11-09)
- [x] HM-0.7 Build and run all three test hosts plus the SDK and dashboard tests to confirm the bump broke nothing. (2026-11-09)

## Phase 1: Port and settings foundation

The phase changes no request behavior. It exists so every later phase can lean on settings that already load, validate, migrate, and render everywhere. It is also the first moment users notice anything, because the port changes, so the documentation and the one-time notice land here and not later.

### Backend and settings

- [x] HM-1.1 Change the default in `src/Xeno.Client.Core/ClientLocalInferenceServerSettings.cs` (lines 11, 26, 36) from 11434 to 11435, including the out-of-range clamp fallback. Document default, minimum, and maximum in the XML docs. (2026-11-09)
- [x] HM-1.2 Add the one-time migration in `ClientSettings.ApplyDefaults` (`src/Xeno.Client.Core/ClientSettings.cs:263`): stored 11434 becomes 11435, any other value is untouched, and `client.json` is rewritten. Record the migration so it can be surfaced once (see HM-1.10). (2026-11-09)
- [x] HM-1.3 Add `RoutingMode`, `Routes`, `DiscoverLocalModels`, `HealthCheckIntervalMs`, `LocalQueueTimeoutMs` to `ClientLocalInferenceServerSettings` with backing fields, clamping, and XML docs. New enum `LocalInferenceRoutingModeEnum` and new class `LocalInferenceRouteRule`, one type per file. (2026-11-09)
- [x] HM-1.4 Add `DefaultMaxConcurrentRequests` to `ClientSettings`, `ShareWithNetwork`, `RouteLocalRequests`, and nullable `MaxConcurrentRequests` to `ClientRunnerSettings`, and nullable `MaxConcurrentRequests` to `ClientModelSettings`, with backfill for existing settings (nulls mean inherit). Add `ConcurrencyLimitResolver` in `Xeno.Client.Core` that returns the effective model and provider limits, so every surface shows the same numbers. (2026-11-09)
- [x] HM-1.5 Extend `ClientSettingsDefaultProbe.cs` (`ClientLocalInferenceServerSettingsProbe` and the provider probe) so missing fields are detected and backfilled rather than defaulted silently. (2026-11-09)
- [x] HM-1.6 Validate route rules on load and on save: model required, provider must exist when `Target` is `Local`, no duplicate normalized model names. Validation errors are per field so the forms can show them. (2026-11-09)
- [x] HM-1.7 Make the worker respect `ShareWithNetwork = false`: those providers' models are not announced to the server and the worker refuses network requests for them. (2026-11-09)
- [x] HM-1.8 Keep the provider loop guard (`ValidateProviderEndpointAgainstLocalInference`, `ClientSettings.cs:213`) working against the new port, and extend it to reject a provider whose endpoint is the listener even when the listener port is non-default. (2026-11-09)
- [x] HM-1.9 Improve the port conflict message in `LocalInferenceServerRuntime.BuildStartupFailureMessage` (`IsPortConflict`, line 73): when the port is busy, probe `GET /api/version` on it and, if Ollama answers, say so and suggest changing Xeno's port, not Ollama's. (2026-11-09)
- [x] HM-1.10 Add `LocalInferencePortMigratedFrom` (nullable int) and a matching acknowledge method to `ClientConnectionState` so front ends can show the notice once. (2026-11-09)
- [x] HM-1.11 Update the provider endpoint default in `ClientRunnerSettings.cs:43` and `XenoForms.cs:302` only if they refer to the listener. Both appear to mean "the real Ollama on 11434" and should stay. Confirm and note the outcome in the log. (2026-11-09)
- [x] HM-1.12 Update `Xeno.DataLoader/SyntheticClientFactory.cs:115` if it represents the listener port. (2026-11-09)

### Presentation (shared)

- [x] HM-1.13 `XenoSettingsCatalog.cs` (lines 38 to 43, `Inference()` helper at 149): update the port help text ("Default 11435. Ollama uses 11434."), add `inference.routingMode`, `inference.discoverLocalModels`, `inference.healthInterval`, `inference.localQueueTimeout`. Mark port as restart-required. (2026-11-09)
- [x] HM-1.14 `XenoForms.AddProvider` / `EditProvider`: add "Share with the Xeno network", "Use for local requests", and "Max concurrent requests" (blank inherits the global default, which the help text shows) with help text explaining the Ollama Cloud case. The model form gets "Max concurrent requests" (blank inherits the provider). `XenoSettingsCatalog` gets `concurrency.defaultMax` for the global level. (2026-11-09)
- [x] HM-1.15 `LocalSettingsEditor.Apply` and `LocalClientSettingsUpdateRequest`: accept the new fields. Settings responses report port as restart-required. (2026-11-09)

### Front ends

- [x] HM-1.16 CLI: `xeno status` (`PrintLocalInferenceStatus`, `Program.cs:4261`) shows routing mode and the one-time port migration notice. Status JSON (`Program.cs:4109`) gains `routingMode` and `portMigratedFrom`. `PrintLocalInferenceAccess` (4212) prints the new URL. (2026-11-09)
- [x] HM-1.17 Local web dashboard (`dashboard/src/local/LocalDashboardApp.jsx`): settings form (1003 to 1027, 1100 to 1108) gains the new fields; port examples at 870, 1023, 1105 use 11435; provider form gains the three flags; migration notice banner shown once. Fix the existing timeout fallback of 120000, which should be 300000. (2026-11-09)
- [x] HM-1.18 Local dashboard API Explorer: `localInferenceSamples` (965) and `buildLocalInferenceCurl` (984) use the configured port, never a hard-coded one. (2026-11-09)
- [x] HM-1.19 Desktop (`src/Xeno.Desktop/Pages/SettingsPage.cs`, `OverviewPage.cs` 195/200) and terminal (`src/Xeno.Tui/Pages/SettingsPage.cs`, `OverviewPage.cs:169`): render the new catalog fields and provider form fields; show the migration notice once (`TuiContext.Notify` in the terminal app). (2026-11-09)
- [x] HM-1.20 Tray (`src/Xeno.Tray`): add a status line with the listener URL and a "Copy local endpoint URL" menu item. The tray has no local inference surface today. (2026-11-09)
- [x] HM-1.21 Add dashboard i18n keys in `dashboard/src/i18n/resources.js` for every new label, help text, and notice. No hard-coded strings. (2026-11-09)

### Tests (`src/Test.Shared`)

- [x] HM-1.22 Update the existing port assertions: `SharedTestSuites.cs` 194, 199, 203, 246 to 289, 1699, 1790 (`"  Port:             11434"`), 1940; `FrontEndTestSuites.cs` 36 to 79, 643; `TuiTestSuites.cs:333`. Leave assertions that refer to the real Ollama endpoint alone. (2026-11-09)
- [x] HM-1.23 New cases: `ClientSettingsMigratesLegacyLocalInferencePort`, `ClientSettingsKeepsCustomLocalInferencePort`, `ClientSettingsBackfillsRoutingDefaults`, `ClientSettingsBackfillsProviderRoutingFlags`, `ClientSettingsBackfillsConcurrencyLimitsAsInherit`, `ModelLimitAboveProviderLimitWarns`, `RouteRuleValidationRejectsUnknownProvider`, `RouteRuleValidationRejectsDuplicateModels`, `WorkerDoesNotAnnounceLocalOnlyProviders`, `LocalInferencePortConflictNamesOllama`. (2026-11-09)
- [x] HM-1.24 Front-end cases: `SettingsCatalogIncludesRoutingFields`, `ProviderFormsIncludeRoutingFlags`, TUI `SettingsPageSavesRoutingMode` (keyboard and `TuiHarness.Click`), `TrayShowsLocalEndpointUrl`. (2026-11-09)
- [x] HM-1.25 Dashboard tests (`dashboard/tests/local-dashboard-ux.spec.mjs` 38, 51, 116): new port, new fields, migration banner shown once. (2026-11-09)

### Docs and Postman

- [x] HM-1.26 `README.md` (85 to 111, 252, 282) and `getting-started/{MACOS,LINUX,WINDOWS}_CLIENT.md`: new port, a short note on why it moved, and how to point apps at it. (2026-11-09)
- [x] HM-1.27 `AGENTS.md`: change the default port line, and add the routing settings once they exist. (2026-11-09)
- [x] HM-1.28 `DESKTOP_AND_TERMINAL.md` (110, 116, 261): new port and settings. (2026-11-09)
- [x] HM-1.29 Postman `Xeno.postman_collection.json` (122, 178): `localInferenceBaseUrl` default becomes `http://127.0.0.1:11435`. (2026-11-09)
- [x] HM-1.30 `CHANGELOG.md` under `## Unreleased`: port change and migration, called out as a behavior change. The version is already 0.2.0 (Phase 0). (2026-11-09)

### CI

- [x] HM-1.31 Add `.github/workflows/test.yaml` per `BACKEND_TEST_ARCHITECTURE.md` "Continuous Integration": on every push to every branch and every pull request, on `ubuntu-latest`, `windows-latest`, and `macos-latest`, run `dotnet build src/Xeno.sln`, `Test.Automated` (with JSON results), `Test.Xunit`, `Test.Nunit`, the dashboard tests, and the C#, JavaScript, and Python SDK harnesses. No filters. Upload results with `if: always()`. (2026-11-09)
- [x] HM-1.32 Decide AOT support for the client and server, and record it under an `AOT support` heading in `README.md`. If either is AOT capable, add `Test.Aot` and run it in the workflow on all three operating systems. (2026-11-09)
- [~] HM-1.33 Make the suite pass in CI as it stands before Phase 1's code lands, so later failures belong to this plan. Record any quarantined case in the log with a tracking issue. (2026-11-09)

- [x] HM-1.34 Fix the two server dashboard Playwright cases that already fail on `main` (see the progress log for 2026-11-09), so CI starts green. (2026-11-09)

**Exit criteria:** CI is green on all three operating systems; all three test hosts pass; the dashboard tests pass; a client upgraded from an existing `client.json` with port 11434 starts on 11435 and every front end shows the notice once; with Ollama running on 11434 the listener starts cleanly.

## Phase 2: Direct local routing

The phase where the product changes. The listener stops being a pipe, and three rules from today's `AGENTS.md` stop being true as written: "every request goes through the tunnel", "tunnel down means 502", and "listings come from the server". Update `AGENTS.md` in the same change as the code, not after.

Passthrough means the request body is forwarded as bytes without being parsed beyond the DTO the route already deserializes for the model name and stream flag. That keeps the "no `JsonNode`" rule intact and avoids silently dropping fields the typed DTOs do not model.

### Core and backend

- [x] HM-2.1 Add `LocalInferenceRouter` in `Xeno.Client.Core` with `ResolveAsync(string model, LocalInferenceApiShapeEnum shape, CancellationToken token)` returning a `LocalInferenceRouteDecision` (target kind, provider name, reason code, fallback allowed, normalized model). Reason codes are stable strings (`rule`, `preferLocal`, `notLocal`, `localUnhealthy`, `networkOnlyMode`, `localOnlyMode`) so front ends can translate them. (2026-11-09)
- [x] HM-2.2 Add `LocalModelNameNormalizer` implementing the Ollama `:latest` rule. Used by the router, the catalog merge, and route rule validation. (2026-11-09)
- [x] HM-2.3 Add `LocalBackendHealthMonitor` in `Xeno.Client.Core`: probes every routable provider at `HealthCheckIntervalMs` (reuse the probe logic in `ProviderConfiguration.TestProviderAsync`, line 467), caches status, last latency, last error, and discovered model list. Passive failures from real requests also mark a backend unhealthy. (2026-11-09)
- [x] HM-2.4 Add `LocalProviderProxy` in `src/Xeno.Client/LocalInference`: `HttpClient` passthrough to `provider.Endpoint + path`, streaming the response as it arrives, propagating cancellation to the upstream request, preserving status code and content type, and forwarding only the safe response header set the tunnel path already uses. The provider `Credential` is sent as the upstream auth header and never logged. (2026-11-09)
- [x] HM-2.5 Change `LocalInferenceServerService.HandleProviderRouteAsync` (193 to 298) so each route asks the router, then calls `LocalProviderProxy` or the existing `ProxyAsync` tunnel path. Keep one place that decides. (2026-11-09)
- [x] HM-2.6 Fallback: only when no byte has been written to the caller. Any failure after the first byte ends the response in the caller's error shape (an SSE error event for OpenAI streams, a final NDJSON error object for Ollama streams) and does not retry. (2026-11-09)
- [x] HM-2.7 Ollama-shaped request to a vLLM or OpenAI-compatible provider returns a provider-shaped 400 naming the model and suggesting the `/v1` route. Phase 4 replaces this. (2026-11-09)
- [x] HM-2.8 Add `X-Xeno-Route` (`local:<provider>` or `network`) and `X-Xeno-Fallback: true` (only when a fallback happened) response headers. Document both, and add them to the CORS exposed headers. (2026-11-09)
- [x] HM-2.9 Add `ExcludeRequestingClient` (bool, default false) to `ClientLocalInferenceRequestMessage` in `src/Xeno.Core/Models`. The server honors it when choosing a provider in `RelayClientLocalInferenceRequestAsync` (`XenoServer.cs:4069`). Old servers ignore the field, so note the degraded behavior in the CHANGELOG. (2026-11-09)
- [x] HM-2.10 Merged listings, built client-side in `Xeno.Client.Core` (`LocalModelListingBuilder`): (2026-11-09)
  - `/v1/models`: local models plus the network list from the tunnel, deduped by normalized name, local wins.
  - `/v1/models/{model}`: local first, then network.
  - `/api/tags`: local models from every routable provider plus the network's Ollama-family list (today's server filter in `BuildOllamaTagsResponseAsync`, `XenoServer.cs:2881`). vLLM models appear only after Phase 4.
  - `/api/show`: forwarded to the local Ollama that serves the model, otherwise the network.
  - `/api/ps`: local Ollama backends only, merged.
  - If the tunnel is down, listings return the local half with status 200. If the local backends are all down and the tunnel is up, the network half. Only when both are unavailable does the listing return a provider-shaped 502.
- [x] HM-2.11 Extend `ModelCatalogBuilder` (`BuildLocal`, `BuildAsync`, `Merge`) and `ModelCatalogEntry` with `RouteTarget`, `RouteReason`, `LocalOnly`, and `Discovered` so every front end shows the same answer the router would give. Discovered models come from `LocalBackendHealthMonitor`. (2026-11-09)
- [x] HM-2.12 Add response DTOs needed for merging (`OpenAiModelListResponse`, `OllamaTagsResponse`, `OllamaPsResponse` and their items) under `LocalInference/Models`, reusing the telemetry DTOs in `Client.Core/Telemetry/Models` where they already match. (2026-11-09)
- [x] HM-2.13 Logging: request metadata only (model, route, status, latency, fallback). No prompt, chat, or embedding content. Replace the `Console` line in `ProxyAsync` (line 333) with the existing client logger if it is library code. (2026-11-09)

### Tests

- [x] HM-2.14 Add `FakeOllamaServer` and `FakeOpenAiServer` to `src/Test.Shared`, bound to `127.0.0.1` on an ephemeral port, scriptable per test (status, body, stream chunks, delay, drop mid-stream, record received requests and cancellation). The existing `FakeOllamaPullServer` only serves `/api/pull`, so extend it or replace it. (2026-11-09)
- [x] HM-2.15 Router cases: `RouterPrefersLocalProvider`, `RouterHonorsNetworkRule`, `RouterHonorsProviderRule`, `RouterNormalizesLatestTag`, `RouterUsesProviderOrderForDuplicates`, `RouterSkipsProvidersNotRoutable`, `RouterNetworkOnlyMatchesLegacyBehavior`, `RouterLocalOnlyNeverUsesTunnel`. (2026-11-09)
- [x] HM-2.16 Proxy cases: `LocalRouteProxiesOpenAiChatToOllama`, `LocalRouteStreamsOpenAiSse`, `LocalRouteStreamsOllamaNdjson`, `LocalRoutePreservesStatusAndContentType`, `LocalRouteFiltersUnsafeHeaders`, `LocalRoutePropagatesCancellation`, `LocalRouteSendsProviderCredential`, `LocalRouteSetsRouteHeader`. (2026-11-09)
- [x] HM-2.17 Fallback cases: `FallbackUsesNetworkWhenEnabled`, `FallbackSetsExcludeRequestingClient`, `NoFallbackReturnsProviderShaped502`, `NoFallbackAfterFirstByte`, `TunnelDownDoesNotBreakLocalRoutes`, `LocalDownDoesNotBreakNetworkRoutes`. (2026-11-09)
- [x] HM-2.18 Listing cases: `ModelsListMergesLocalAndNetwork`, `TagsListDedupesLocalWins`, `PsListsLocalOnly`, `ShowForwardsToLocalOllama`, `ListingsSurviveTunnelOutage`, `ListingsReturn502WhenEverythingDown`. (2026-11-09)
- [x] HM-2.19 `OllamaShapeToVllmReturnsGuidance400` (removed or inverted in Phase 4). (2026-11-09)
- [x] HM-2.20 `LocalRoutingNeverLogsPromptContent`: send a sentinel string in a prompt and assert it never appears in captured log output. (2026-11-09)
- [x] HM-2.21 Server relay case in `RelaySuite`: `ExcludeRequestingClientSkipsOrigin`. (2026-11-09)
- [x] HM-2.22 Update `ModelCatalogMergesLocalAndNetworkModels` (`FrontEndTestSuites.cs:76`) and add `CatalogMarksRouteTargets`, `CatalogIncludesDiscoveredLocalModels`, `CatalogMarksLocalOnlyProviders`. (2026-11-09)

### Docs

- [x] HM-2.23 `AGENTS.md` Local Inference Server section: describe routing, the 502 rule per backend, client-built listings, the passthrough allowance, and `X-Xeno-Route`. (2026-11-09)
- [x] HM-2.24 `WEBSOCKETS_API.md`: document the local inference messages, which are missing today, including `ExcludeRequestingClient`. (2026-11-09)
- [x] HM-2.25 `README.md`: a short "One endpoint for local and network models" section with the Ollama plus Ollama Cloud example. (2026-11-09)

**Exit criteria:** with a fake Ollama and a fake tunnel, every case above passes on all three hosts; a real local Ollama serves an OpenAI SDK pointed at `127.0.0.1:11435` with `X-Xeno-Route: local:local-ollama`; `RoutingMode = NetworkOnly` produces byte-identical behavior to Phase 1 for the existing tunnel tests.

## Phase 3: Shared concurrency and visibility

Phase 2 makes routing work. Phase 3 makes it safe on a machine that also serves the network, and makes it visible enough that a user can tell why a request went where it went. Most of the front-end work in this plan sits here, and every surface below renders the same `Xeno.Client.Core` data, so build the core types first and the pages second.

### Core and backend

- [x] HM-3.1 Add `ProviderConcurrencyGate` in `Xeno.Client.Core`: one async semaphore per provider and one per model on that provider, sized by `ConcurrencyLimitResolver` (HM-1.4), acquired model first then provider, shared by the worker (`Program.ExecuteServerRequestAsync`) and `LocalProviderProxy`. Resizes live when the setting changes. Exposes in-flight and queued counts. (2026-11-09)
- [x] HM-3.2 Local requests wait up to `LocalQueueTimeoutMs`, then get a provider-shaped 429 with `Retry-After`. Cancellation while queued releases nothing and returns promptly. (2026-11-09)
- [x] HM-3.3 Make the server see local load. Today the server only counts its own dispatches: `RoutingService` (`src/Xeno.Core/Services/RoutingService.cs:307`) excludes a client when `InFlightRequests >= MaxParallelRequests` (a per-client value stored on `XenoClientRecord`, default 4, not reported by the client), and when every candidate is excluded it returns 502 `NoEligibleClients` with no queue and no retry. Once local requests share the gate, the server would keep sending work to a machine that is busy serving its owner. Fix: the client reports its effective capacity (sum of shared provider limits from `ConcurrencyLimitResolver`) and its free slots per model in the heartbeat or the HM-5.5 capacity message; `RoutingService` uses the reported free slots for eligibility and scoring when present and falls back to today's counter for older clients. A network request that still finds the gate full fails fast with a 503 before any byte is sent, and the server retries the next candidate once (bounded, recorded in the routing timeline) before answering the caller. Add a `RelaySuite` case for the retry and a routing case for reported capacity. (2026-11-09)
- [x] HM-3.4 Add a local inference request log in `Xeno.Client.Core` (`LocalInferenceRequestLog`): metadata only (time, method, path, model, route, provider, status, latency, time to first token, prompt and completion tokens when the response reports them, bytes, fallback, queued milliseconds), bounded in memory with an on-disk ring under `~/.xeno` that follows the existing `ClientLocalHistorySettings` retention. Bodies are never stored, whatever `PersistBodies` says, because no diagnostic setting permits prompt capture for the listener. Phase 5 (HM-5.17) feeds these rows into the client usage rollups, so capture every field the usage charts need now. (2026-11-09)
- [x] HM-3.5 Add `LocalInferenceRoutingSnapshot` to `ClientStatusSnapshot` (`ClientStatusSnapshot.cs` 137 to 157, 267 to 273): mode, rules, backends with health and in-flight/limit, and counters for local, network, fallback, and errors since start. (2026-11-09)
- [x] HM-3.6 Add `XenoWorkspace` methods: `GetLocalInferenceRoutingAsync`, `ResolveRouteAsync`, `SaveRouteRuleAsync`, `DeleteRouteRuleAsync`, `CheckBackendsAsync`, `GetLocalInferenceRequestsAsync`. (2026-11-09)
- [x] HM-3.7 Local dashboard API in `LocalDashboardService`, matching existing route naming in that service (confirm before coding): list routing state, resolve a model, create/update/delete a route rule, trigger a health check, list recent local inference requests with pagination. Validation errors per field; secrets reported only as set or not set. These live on the dashboard API, not on the listener, because the listener's route list is fixed by `AGENTS.md`. (2026-11-09)
- [x] HM-3.8 Client telemetry: route outcome counters (`local`, `network`, `fallback`, `error`) and latency per provider, following whatever `ClientProviderTelemetryService` already emits. Model names are not used as metric labels. No secrets in labels. (2026-11-09)

### Presentation (shared)

- [x] HM-3.9 Add `XenoPageEnum.LocalEndpoint` and its entry in `XenoNavigationCatalog` ("Local endpoint": the URL apps use, where requests go, and backend health). (2026-11-09)
- [x] HM-3.10 `XenoMenuLayout` and `XenoCommandAvailability`: commands `AddRouteRule`, `EditRouteRule`, `DeleteRouteRule`, `TestRoute`, `CheckBackends`, `CopyLocalEndpointUrl`. Disabled with a reason when the listener is off. (2026-11-09)
- [x] HM-3.11 `XenoForms.RouteRule`: model (with suggestions from the catalog), target (Local provider or Xeno network), provider, fallback toggle with help text explaining it never retries mid-stream. (2026-11-09)
- [x] HM-3.12 `XenoCharts`: a "Local endpoint traffic" chart (local vs network vs fallback over time). Add it here first, then the web dashboard. Phase 5 moves this chart onto the shared usage model and palette (HM-5.23, HM-5.24); keep it simple here. (2026-11-09)
- [x] HM-3.13 `XenoMarkdown`: a route explanation builder for "why did model X go here", values escaped. (2026-11-09)
- [x] HM-3.14 `XenoDiagnostics.cs:76`: extend the "Local inference" line with mode and backend health. (2026-11-09)

### Front ends

- [x] HM-3.15 CLI: `xeno status` gains a routing block (mode, rules count, each backend with health and in-flight/limit, counters) and the JSON equivalent. New commands: `xeno inference routes` (list), `xeno inference resolve <model>`, `xeno inference route set <model> --local <provider> | --network [--fallback]`, `xeno inference route remove <model>`, `xeno inference backends` (health, `--check` to probe now). Add them to the help text (`Program.cs:4794`) and `PrintConfig` (3886), which does not show the listener today. (2026-11-09)
- [x] HM-3.16 Local web dashboard: a "Local endpoint" page with a listener card (URL with copy control, status, mode), a backends table (provider, type, health, latency, in-flight/limit, shared, routable), a routes table (model, source, target, reason, fallback) with `+ Add`, Select Columns, pagination above the table, and row actions View, Edit, Duplicate, View JSON, Delete; clicking a row opens Edit. Recent requests table with the same controls. Custom confirmation modals. Loading, empty ("no route rules yet, local models are routed automatically"), error, and partial-failure states ("network unavailable, showing local models only"). Auto-refresh pauses while a modal is open. (2026-11-09)
- [x] HM-3.17 Local dashboard home: KPIs for local requests, network requests, fallbacks, and unhealthy backends, plus the traffic chart from HM-3.12. (2026-11-09)
- [x] HM-3.18 Local dashboard API Explorer: a route resolver ("where would this model go?") next to the model field, the response headers panel shows `X-Xeno-Route` and `X-Xeno-Fallback`, streaming has a Stop control, and generated snippets use the configured port. (2026-11-09)
- [x] HM-3.19 Desktop (`src/Xeno.Desktop`): `LocalEndpointPage` built in code (no XAML), using the shared tables, dialogs, and `ColumnChart` from `Xeno.Desktop.Common`. `ModelsPage` and `OverviewPage` show route target per model. (2026-11-09)
- [x] HM-3.20 Terminal (`src/Xeno.Tui`): `LocalEndpointPage` through `TuiPage.Frame`, tables and forms that work with keyboard and mouse, dropdowns through `DropdownPicker`, deletes confirmed through `ChoiceDialog`, the route explanation shown with `TuiContext.ShowMarkdownAsync`, progress and results through `TuiContext.Progress` and `TuiContext.Notify`. Fits within 120x36. (2026-11-09)
- [x] HM-3.21 Tray (`src/Xeno.Tray`): status shows mode and a backend health summary ("2 of 2 local backends healthy"); menu item "Open local endpoint" opens the desktop page; optional notification when a routable backend becomes unhealthy, off by default. (2026-11-09)
- [x] HM-3.22 i18n: dashboard keys for every new label, state, and reason code; reason codes and health states arrive from the backend as stable codes and are translated in the UI. Desktop and terminal apps follow whatever string handling they use today; record in the log whether that is localized, and do not introduce a new pattern in this plan. (2026-11-09)
- [x] HM-3.23 Visual QA of the dashboard pages at 1280, 768, and 390 px in light and dark themes. (2026-11-09)

### Tests

- [x] HM-3.24 Concurrency cases: `LimitsResolveModelThenProviderThenGlobal`, `ModelLimitCapsOneModelOnly`, `ProviderLimitCapsAllModels`, `GateIsSharedByWorkerAndListener`, `LocalRequestQueuesThenSucceeds`, `LocalRequestQueueTimeoutReturns429`, `NetworkRequestFailsFastWhenGateFull`, `GateResizesLive`, `QueuedRequestCancelsPromptly`. (2026-11-09)
- [x] HM-3.25 Request log cases: `RequestLogRecordsMetadataOnly`, `RequestLogNeverStoresBodies`, `RequestLogHonorsRetention`. (2026-11-09)
- [x] HM-3.26 Status and CLI cases: `StatusShowsRoutingBlock`, `StatusJsonIncludesRouting`, `CliResolvePrintsDecision`, `CliRouteSetAndRemove`, `CliRouteSetRejectsUnknownProvider`. (2026-11-09)
- [x] HM-3.27 Dashboard API cases: list, resolve, create, update, delete, validation errors per field, pagination. (2026-11-09)
- [~] HM-3.28 Front-end cases: `NavigationCatalogCoversEveryPage` (update), `RouteRuleFormValidates`, `CommandAvailabilityDisablesRoutingWhenListenerOff`, `LocalEndpointChartMatchesDashboard`, desktop `LocalEndpointPageBuildsHeadlessly`, tray `TrayShowsBackendHealth`. (2026-11-09)
- [x] HM-3.29 TUI cases through `TuiHarness`: `LocalEndpointPageRenders`, `LocalEndpointAddRouteWithKeyboard`, `LocalEndpointAddRouteWithMouse` (`TuiHarness.Click`), `LocalEndpointDeleteConfirmsWithChoiceDialog`, `LocalEndpointExplainShowsMarkdown`. Use `ClientHomeScope`, never the real `~/.xeno`. (2026-11-09)
- [x] HM-3.30 Dashboard tests: the Local endpoint page states (loading, empty, error, partial), row actions, the API Explorer resolver and route header display. (2026-11-09)

### Docs and Postman

- [x] HM-3.31 `REST_API.md`: it does not cover the listener or the local dashboard API today. Add both: every listener route with the routing behavior and route headers, and every new dashboard routing endpoint with method, path, parameters, bodies, status codes, and examples. (2026-11-09)
- [x] HM-3.32 Postman: in the Xeno Client folder, add a "Local Routing" folder (list, resolve, create/update/delete rule, check backends, recent requests) with collection, folder, and request descriptions; add example responses showing `X-Xeno-Route`; keep every URL on variables. (2026-11-09)
- [x] HM-3.33 `DESKTOP_AND_TERMINAL.md`: the Local endpoint page and its commands in both apps and the tray. (2026-11-09)
- [x] HM-3.34 `README.md` and the getting-started guides: configure a local-only provider for Ollama Cloud or `https://ollama.com`, add a route rule, read `xeno status`. (2026-11-09)
- [x] HM-3.35 `AGENTS.md`: add the Local endpoint page and `ProviderConcurrencyGate` to the sync rules. (2026-11-09)
- [x] HM-3.36 `CHANGELOG.md` under `## Unreleased`. (2026-11-09)

**Exit criteria:** every surface (CLI, web dashboard, desktop, terminal, tray) shows the same route for the same model and the same backend health; the concurrency cases pass; a request log entry exists for every listener request and contains no body.

## Phase 4: Shape translation

Translation is the one place this plan writes real protocol code, and the one place a quiet mistake (a dropped `tool_calls`, a mis-mapped `num_predict`) produces answers that look right and are not. Every translator is typed end to end and tested against recorded fixtures from real Ollama and vLLM, not only against the fakes.

- [ ] HM-4.1 Add typed response and stream DTOs for both shapes under `LocalInference/Models` (OpenAI chat completion, completion, embedding, chunk; Ollama chat, generate, embed, embeddings, stream object). One class per file, XML docs on every public member.
- [ ] HM-4.2 `OllamaToOpenAiRequestTranslator`: `/api/chat` to `/v1/chat/completions`, `/api/generate` to `/v1/completions`, `/api/embed` and `/api/embeddings` to `/v1/embeddings`. Map options (`temperature`, `top_p`, `num_predict` to `max_tokens`, `stop`, `seed`), `format: json` to `response_format`, tools and tool calls, images where the backend supports them. Unsupported options are dropped and listed in the debug log by name, never by value.
- [ ] HM-4.3 `OpenAiToOllamaResponseTranslator` for non-streaming responses, including usage to `prompt_eval_count` / `eval_count` and `finish_reason` to `done_reason`.
- [ ] HM-4.4 `SseToNdjsonStreamTranslator`: OpenAI SSE chunks to Ollama NDJSON objects, final `done: true` object with usage, `[DONE]` handling, errors mid-stream mapped to a final Ollama error object. Cancellation propagates both ways.
- [ ] HM-4.5 Synthesize `/api/show` for vLLM-served models from `/v1/models` metadata, and include vLLM models in `/api/tags`.
- [ ] HM-4.6 Remove the HM-2.7 guidance 400 and update `AGENTS.md` to describe the translation allowance and its limits.
- [ ] HM-4.7 Decide whether the server-side relay (Ollama-shaped request from a local app routed to a network vLLM provider) uses the same translators. Today the server filters Ollama-shaped requests to Ollama providers only. Record the decision; if yes, move the translators to `Xeno.Core` so both sides share them.
- [ ] HM-4.8 Tests: one case per mapping in each direction, streaming with 1, many, and zero content chunks, tool calls, JSON format, mid-stream failure, cancellation, unsupported option dropped without its value logged. Fixture files recorded from real Ollama and vLLM checked into `src/Test.Shared`.
- [ ] HM-4.9 Docs: `REST_API.md` compatibility notes per route, Postman examples for an Ollama-shaped request served by vLLM, `CHANGELOG.md`.

**Exit criteria:** the Ollama CLI pointed at the listener (`OLLAMA_HOST=127.0.0.1:11435 ollama run <vllm-model>`) chats with a vLLM-served model, streaming included; every fixture case passes on all three hosts.

## Usage visualization: what we are copying, and what we are not

Mux and Armada were surveyed on 2026-11-09. Both draw every chart by hand (SVG on the web, glyph cells in the terminal, code-built controls on the desktop) with no chart library, and Xeno already works the same way, so the patterns carry over directly. The parts worth taking:

- **From Mux:** a KPI strip that changes with the active chart tab; tabbed charts for tokens, latency, time to first token, streaming, and throughput; candlestick distributions (min to max wick, average to p95 box, average line, p99 tick) instead of single averages; clock-aligned heatmap windows in the terminal app; zero-filled dense series; a per-call history grid under the charts with a column picker, tri-state sort, and View / View JSON row actions; one usage query shape (`summary`, `timeseries`, `breakdown`, `events`, `filters`) with shared filter parameters.
- **From Armada:** an explicit `(other)` row so top-N breakdowns still add up; a hard bucket cap with a typed error; coverage and truncation flags so a chart can say what it is missing; utilization lines with a capacity line and 75 / 90 percent thresholds that always carry a text label; reset and gap handling (never interpolate missing data, never show unknown as zero); copy chart as PNG; a "show chart data table" toggle; auto-refresh selector with per-view persistence and pause; filters in the URL; hourly rollups with separate retention; a parity manifest mapping each web view to its terminal and desktop equivalents; `NO_COLOR` and high-contrast glyph fallbacks.

The parts to avoid are the ones where both products drifted. Mux's desktop computes percentiles with a different method than its server, its heatmap windows differ between the terminal and the web, and its VS Code panel swaps the token colors. Armada's series color cycles differ between two pages. Neither has chart click-through, compare to previous period, or a custom date range in the UI. Xeno gets a single source for windows, percentiles, and colors, every surface only draws what that source produces, and the three missing features are in scope.

## Usage model

Xeno has four audiences for usage, and every view states which one it is showing.

| Perspective | Who | What it shows | Where |
|---|---|---|---|
| Consumer | Any signed-in user | Requests and tokens this user sent to the network, and ledger debits | Server dashboard, local dashboard, desktop, terminal, tray, CLI, SDKs |
| Contributor | Any user who owns a client | Requests and tokens their clients served, utilization of their providers, ledger credits | Same as above |
| Tenant | Tenant admins | Everything in the tenant, broken down by user, credential, client, and model | Server dashboard, CLI, SDKs |
| This machine | The local client | Requests this machine served for the network, and requests through the local endpoint (local route, network route, fallback) | Local dashboard, desktop, terminal, tray, CLI |

Metrics, everywhere they make sense: requests, successes, failures and error rate, prompt tokens, completion tokens, total tokens, latency distribution, time to first token distribution, tokens per second distribution, streaming share, bytes, utilization percent (in-flight over capacity), queued requests and queue wait, 429s, route mix (local, network, fallback), and ledger credits, debits, and net. Cached tokens are not reported by Xeno's providers today, so they are left out rather than shown as zero.

Dimensions for breakdowns and filters: model, client, provider, provider family, user, credential, path, status class, stream, route, and tenant (system admins only).

### One windowing rule

`UsageWindow` in `Xeno.Core` defines every preset once, following the Mux and Armada granularity. The server, the client, and every renderer use it, and no surface computes its own buckets. Heatmaps use the same buckets as the charts, laid out on a clock-aligned grid, so a heatmap cell and a chart bar for the same moment always hold the same number.

| Preset | Span | Buckets | Heatmap rows x columns |
|---|---|---|---|
| Last hour | 1 h | 120 x 30 s (2 per minute) | 4 x 30 (each row 15 min) |
| Last day | 24 h | 96 x 15 min (4 per hour) | 24 x 4 (each row 1 h) |
| Last week | 7 d | 84 x 2 h (12 per day) | 7 x 12 (each row 1 d) |
| Last month | 30 d | 90 x 8 h (3 per day) | 30 x 3 (each row 1 d) |
| Custom | any | span / 90 rounded to 30 s, 1, 5, 15, 30 min, 1, 2, 4, 8, 12 h, 1 d, 1 w | nearest preset layout |

Requests over 1000 buckets fail with a typed 400.

### Time zones

All timestamps and rollup buckets are stored in UTC. Every surface renders in the viewer's local time zone and shows the zone next to the timeframe control. Queries send the viewer's IANA zone (`timeZone`) so the server aligns day and hour boundaries to the viewer's clock. For zones with a non-whole-hour offset (for example UTC+5:30), buckets of an hour or longer align to the nearest boundary the rollup tier supports, and the chart's info tooltip says so.

### Percentiles

Percentiles are computed in one place, from fixed log-scale latency histograms stored on every rollup row (24 bins from 1 ms to 10 minutes). Every surface shows the same p50, p95, and p99 for the same window because they all read the same server or client answer. The result is approximate within one bin width, which the UI states in the chart's info tooltip. No front end computes a percentile.

## Phase 5: Usage data foundation

The phase builds the data and the contracts. It ships no new pages, but the existing charts (`XenoCharts.Requests`, `ActivityChart`, the ledger trend) move onto the new APIs so the old in-memory aggregation can be removed in the same phase.

### Server storage

- [ ] HM-5.1 First real schema migration after the baseline in all four drivers (SQLite, MySQL, PostgreSQL, SQL Server): promote `prompt_tokens`, `completion_tokens`, `duration_ms`, `ttft_ms`, `success`, `stream`, `serving_client_id`, `serving_owner_user_id`, `requestor_user_id`, `credential_id`, and `provider_family` from `entry_json` to columns on `request_history`, with indexes for the usage queries. Backfill from `entry_json` in batches.
- [ ] HM-5.2 Add `usage_rollups_30s` and `usage_rollups_hour` tables (UTC bucket starts) keyed by tenant, requestor tenant, bucket start, model, serving client, serving owner, requestor user, credential, provider family, path, status class, and stream. Columns: requests, successes, failures, prompt, completion, and total tokens, duration sum, TTFT sum, tokens per second sum, bytes in and out, and three histogram columns (duration, TTFT, tokens per second) stored as fixed-width typed columns or a typed encoded value, not JSON.
- [ ] HM-5.3 `UsageRollupWriter`: updates the 30-second rollup on each request-history insert (fire and forget, batched, bounded queue with a dropped-events counter), and a background job folds 30-second rows into hours. Last hour and Last day read the 30-second tier; Last week, Last month, and long custom ranges read the hour tier.
- [ ] HM-5.4 Retention settings in `RequestHistorySettings` and a new `UsageSettings`: raw history 30 days (existing), 30-second rollups 8 days (2 to 31), hour rollups 400 days (31 to 1825). The hour tier outlives the presets on purpose, so custom ranges and the ledger can look back a year. Pruning runs in the existing prune loop (`XenoServer.cs:2995`).
- [ ] HM-5.5 Utilization samples: clients report per-provider capacity (`MaxConcurrentRequests`, from HM-1.4) and in-flight count in the existing heartbeat or a new typed message in `src/Xeno.Core/Models`. The server samples every connected client every 60 seconds into `client_utilization_samples` (client, provider, min, max, mean in-flight, capacity, queued, connected seconds) and rolls them up hourly. Document the message in `WEBSOCKETS_API.md`.
- [ ] HM-5.6 Connected-client history: record connected and total client counts per minute on the server, replacing the browser-side sampling in `ConnectedClientsChart.jsx` (which keeps 500 samples in localStorage and loses them on another browser).
- [ ] HM-5.7 Ledger rollups: credits, debits, and net per user per hour, computed from request history the same way ledger entries are today, so ledger charts do not page through entries.
- [ ] HM-5.8 Replace the in-memory aggregation in `SummarizeRequestHistoryAsync` (`XenoServer.cs:9603`), `AnalyticsOverviewAsync` (`:9470`), and `DatabaseDriverBase.GetModelTokenLeaderboardAsync` (`DatabaseDriverBase.cs:193`) with rollup queries. The response shapes of those existing endpoints stay the same.

### Server API

All new routes live under `/v1.0/api/usage`, use typed request and response DTOs in `Xeno.Core`, accept the shared filter set (`range` or `fromUtc` and `toUtc`, `timeZone`, `perspective`, `model`, `clientId`, `provider`, `providerFamily`, `userId`, `credentialId`, `path`, `status`, `stream`, `route`), and return `coverage` (`truncated`, `rawRetentionLimited`, `unattributedRequests`, `warning`).

- [ ] HM-5.9 `GET /v1.0/api/usage/summary`: KPI metrics for the window plus the same metrics for the previous window of equal length, for compare-to-previous deltas.
- [ ] HM-5.10 `GET /v1.0/api/usage/timeseries`: dense, zero-filled buckets from `UsageWindow`, each with metrics and optional `groupBy` series (top N plus `(other)`, `limit` 1 to 50, default 8).
- [ ] HM-5.11 `GET /v1.0/api/usage/breakdown?dimension=`: one row per value of a dimension with full metrics and percentiles, server-side sort and pagination, `(other)` row.
- [ ] HM-5.12 `GET /v1.0/api/usage/heatmap?metric=requests|tokens|promptTokens|completionTokens|errors|utilization`: the clock-aligned grid from `UsageWindow`, with per-cell values, peak, active-cell count, and the five-level index precomputed so every renderer colors identically.
- [ ] HM-5.13 `GET /v1.0/api/usage/utilization` (per client and provider: mean and peak percent, capacity line, queued, threshold state as a stable code), `GET /v1.0/api/usage/events` (paged request rows with the usage columns, no bodies), `GET /v1.0/api/usage/filters` (values available in the window), and `GET /v1.0/api/usage/export?format=csv|json` (the table behind any chart, row cap 100000).

### Authorization

- [ ] HM-5.14 Add a `Usage` permission resource. Seed it in `XenoPlatform.cs` (around 630 to 665): TenantMember gets Usage Read scoped to their own consumer and contributor perspectives; Viewer gets Usage Read for the tenant; TenantAdmin gets everything in the tenant; system admins can query across tenants. Today TenantMember cannot read request history at all, so members currently see no usage; this fixes that without exposing other users' rows.
- [ ] HM-5.15 Enforce the perspective on every usage route: a member asking for `perspective=tenant` or another `userId` gets 403 with the standard authorization failure body. The user dimension is hidden from breakdowns for non-admins.
- [ ] HM-5.16 Public model leaderboard: the global network leaderboard stays public; a tenant's leaderboard is public only when a tenant admin enables `PublicLeaderboard` on the tenant (default off, so existing public tenant queries stop working unless opted in, noted in the CHANGELOG). Unauthenticated requests for a tenant that has not opted in get 404, not 403, so tenant existence does not leak. The leaderboard reads rollups via HM-5.8.

### Client storage and API

- [ ] HM-5.17 Client usage rollups in the client SQLite store: 30-second and hour tables like HM-5.2, fed by two streams: requests this machine served for the network (`local_request_history`) and requests through the local endpoint (HM-3.4). Each row carries `stream` (`served` or `endpoint`) and, for endpoint rows, `route` (`local`, `network`, `fallback`).
- [ ] HM-5.18 Client retention: add age-based retention to `ClientLocalHistorySettings` (default 30 days) alongside `MaxEntries`, because 1000 rows cannot back a month chart; rollups follow the server's tier defaults.
- [ ] HM-5.19 Client utilization samples from `ProviderConcurrencyGate` (HM-3.1) every 15 seconds: in-flight split by local and network, queued, capacity, queue wait histogram.
- [ ] HM-5.20 Local dashboard usage API under `/api/v1/usage/*` with the same paths, parameters, and DTOs as the server API (perspective is always `machine`), so the web components and the `Xeno.Client.Core` view models work against either.
- [ ] HM-5.21 `XenoServerClient`: methods for every server usage route, so the desktop app, terminal app, tray, and CLI can show the signed-in user's consumer and contributor usage next to this machine's.

### Shared presentation model

- [ ] HM-5.22 Extend `XenoChart` (`src/Xeno.Client.Core/Presentation/XenoChart.cs`) into a small chart catalog: `XenoColumnChart` (stacked or grouped), `XenoLineChart` (with gap segments, reference lines for capacity and thresholds, dashed estimated series), `XenoDistributionChart` (candlestick), `XenoHeatmap`, `XenoBarList` (horizontal top-N with `(other)`), `XenoMeter` (utilization with threshold state), `XenoSparkline`, and `XenoKpi` (value, unit, previous value, delta, delta direction meaning). Each carries title, range text, unit, empty text, an accessible text summary, and a tabular form for the data table toggle.
- [ ] HM-5.23 `XenoUsageCharts` in `Xeno.Client.Core` builds every usage chart from the usage DTOs. The desktop app, terminal app, and tray render only what it returns. The web dashboard gets the same definitions from a JSON contract (HM-5.25), not from a reimplementation.
- [ ] HM-5.24 One palette, `assets/chart-palette.json`, consumed by `XenoTheme.cs`, `XenoTuiTheme.cs`, and `dashboard/src/index.css` (generated variables): semantic tones (accent, success, danger, warning, info, idle), an eight-color series cycle validated for contrast on both themes, fixed token-type colors (prompt, completion), fixed route colors (local, network, fallback), the five-step heat ramp for light, dark, and high contrast, and threshold colors. The dark theme today shares the light theme's strong hues (`index.css`), which is a contrast bug this fixes.
- [ ] HM-5.25 Series color assignment rule, defined once: within a view, series are ranked by the view's primary metric and take cycle colors in rank order; `(other)` is always the idle tone; the same series keeps its color across every chart in the view.
- [ ] HM-5.26 Thresholds in one place: utilization below 75 percent normal, 75 to 90 warning, 90 and above danger, each with a text label. Error rate thresholds likewise. No state relies on color alone.
- [ ] HM-5.27 A parity manifest, `src/Xeno.Client.Presentation/usage-parity.json`, listing every web usage view and its desktop, terminal, tray, and CLI equivalent with a status, checked by a test so a new web view without a counterpart fails the build.

### Tests

- [ ] HM-5.28 Database matrix cases for every driver: migration and backfill, rollup writes, minute-to-hour fold, retention pruning per tier, utilization sampling, ledger rollups.
- [ ] HM-5.29 `UsageWindow` cases: 120, 96, 84, and 90 buckets for the four presets with correct widths, heatmap grid shape matching each preset, custom span rounding, UTC storage with local-zone alignment across a DST change and in a UTC+5:30 zone, the 1000-bucket cap.
- [ ] HM-5.30 Percentile cases: histogram bins, p50, p95, p99 against known distributions within one bin width, empty and single-sample windows.
- [ ] HM-5.31 Leaderboard cases: global public, tenant opted out returns 404 unauthenticated, tenant opted in public, authenticated members always see their own tenant.
- [ ] HM-5.39 API cases for each usage route: filters, perspectives, previous-window values, `(other)` totals adding up, coverage flags when raw retention is exceeded, CSV and JSON export, 403 for members asking for tenant or another user.
- [ ] HM-5.32 Equivalence cases: existing `request-history/summary`, `analytics/overview`, and leaderboard responses are unchanged after HM-5.8 for the same seeded data.
- [ ] HM-5.33 Client cases: both streams feed rollups, route split, age retention, utilization samples, and the local usage API matches the server DTOs field for field.
- [ ] HM-5.34 Presentation cases: palette loads identically into C# and the generated CSS, series color assignment, threshold labels, every chart's accessible summary and table form, parity manifest completeness.

### Docs and Postman

- [ ] HM-5.35 `REST_API.md`: the server usage routes and the local usage routes with parameters, DTOs, status codes, and examples. `WEBSOCKETS_API.md`: the capacity and utilization message.
- [ ] HM-5.36 Postman: a "Usage" folder under Xeno Server and under Xeno Client, every route, documented, variables only.
- [ ] HM-5.37 `OBSERVABILITY.md` and `PROMETHEUS_GRAFANA.md`: new metrics (rollup queue drops, utilization gauges) and the relationship between the Prometheus view (for operators) and the usage API (for users).
- [ ] HM-5.38 SDKs (`src/sdk/csharp`, `javascript`, `python`): usage methods for every server route, typed models, tests in each harness using `127.0.0.1`, README sections.

**Exit criteria:** all four database drivers pass the matrix; the existing charts render from the new APIs with identical numbers; old in-memory aggregation is removed.

## Phase 6: Usage visualization

Every surface below gets the same set of views, adapted to what the surface can show. The web dashboards carry the full experience. The desktop and terminal apps match it view for view. The tray carries a glanceable summary. The CLI carries tables and exports. The parity manifest (HM-5.27) is the checklist.

### The usage view, defined once

Each usage page has the same structure, top to bottom:

1. **Header controls**: perspective (where more than one applies), timeframe (Last hour, Last day, Last week, Last month, Custom with a start and end picker; times shown in the viewer's zone), filters (model, client, provider, user, credential, route, status, stream; only those the perspective allows), group-by, compare-to-previous toggle, auto-refresh selector (Off, 15, 30, 60, 120, 300 seconds; default 15; persisted per view; paused while a modal is open or the window is hidden; "Updated 12 s ago" indicator; manual refresh button), and export (PNG for the active chart, CSV and JSON for the table). Filters, range, and tab live in the URL on the web.
2. **KPI strip** that changes with the active tab, each tile showing value, unit, and the delta against the previous window when compare is on, with direction colored by meaning (more errors is bad, more tokens served is good).
3. **Chart tabs**: Tokens (stacked prompt and completion, or by model), Requests (success and failure, or by status class), Latency (candlestick), Time to first token (candlestick), Throughput (tokens per second candlestick), Heatmap (metric selector), Top models (bar list), Utilization (lines per provider with capacity and thresholds), Route mix (local, network, fallback; local and machine perspectives only), Ledger (credits, debits, net line; consumer and contributor perspectives).
4. **Chart interactions**: hover tooltip with bucket range, every series value, and the total; clicking a bucket, heatmap cell, or bar narrows the table below to that slice and shows a removable chip; a "show data table" toggle under each chart; keyboard navigation of buckets and cells with an `aria-live` readout; a stacked or lines toggle where both make sense.
5. **Supporting tables**: a breakdown table for the current group-by (dimension, requests, tokens, error rate, p50, p95, p99 latency, TTFT p95, tokens per second, share of total) and a request table (time, model, client or route, user where allowed, status, prompt, completion, total tokens, latency, TTFT, tokens per second, stream). Both: pagination above the table, server-side sort, Select Columns saved per view, row actions View and View JSON, empty, loading, error, and partial states, and the chart click-through chip.

### Server dashboard (`dashboard/src/App.jsx`)

- [ ] HM-6.1 New Usage page with the structure above and the consumer, contributor, and tenant perspectives. Merge the current Analytics page (`:3699`) into it, keeping the raw `/metrics` text as an operator tab, and render the `byClient` breakdown that is returned but not shown today.
- [ ] HM-6.2 Home (`:1323`): KPI tiles for tokens and requests with deltas, a 24-hour tokens sparkline, a utilization meter for the user's clients, and links into Usage with the matching filters.
- [ ] HM-6.3 Clients page: a utilization column with a sparkline and threshold label; client detail gets a utilization panel (lines with capacity, queued, connected-time strip in the style of Armada's link health) and a usage panel filtered to that client.
- [ ] HM-6.4 Ledger page (`:3763`): replace the balance trend with the ledger tab chart (credits, debits, net line) from rollups; KPI strip for balance, credits, debits with deltas; earnings by model bar list.
- [ ] HM-6.5 Model Leaderboard (`:3310`) and the public leaderboard: render from rollups with the shared timeframe control and bar list; add the tenant `PublicLeaderboard` toggle to the tenant settings page, with a preview of what the public sees.
- [ ] HM-6.6 Connected clients chart: switch `ConnectedClientsChart.jsx` to the server history from HM-5.6 and delete the localStorage sampling.
- [ ] HM-6.7 Client Telemetry page (`:527`): move its spark charts and `PerformanceMetricBarChart` onto the shared chart components and palette, so telemetry and usage look like one product.
- [ ] HM-6.8 Shared web components in `dashboard/src/components/usage/`: `UsageToolbar`, `TimeframeSelector` (with custom range), `AutoRefreshSelect`, `KpiStrip`, `ColumnChart`, `LineChart`, `DistributionChart`, `Heatmap`, `BarList`, `Meter`, `Sparkline`, `ChartDataTable`, `ChartExportButton`, `BreakdownTable`, `UsageRequestTable`. Replace `ActivityChart.jsx`, `TelemetrySparkChart`, `MetricChart`, and `TimeRangeSelector`/`TimeRangeButtons` with them. No chart library.

### Local dashboard (`dashboard/src/local/LocalDashboardApp.jsx`)

- [ ] HM-6.9 Usage page with two perspectives on this machine (Served for the network, Local endpoint) plus, when signed in, the user's network consumer and contributor views through the client's server connection. Same components as HM-6.8.
- [ ] HM-6.10 Local home: KPIs for served and endpoint traffic with deltas, route mix sparkline, utilization meter per provider.
- [ ] HM-6.11 Local endpoint page (HM-3.16): replace its chart with the Route mix and Utilization tabs from the shared components, and add the queue wait candlestick from HM-5.19.
- [ ] HM-6.12 Provider telemetry page: shared chart components and palette.

### Desktop (`src/Xeno.Desktop`, `src/Xeno.Desktop.Common`)

- [ ] HM-6.13 Chart controls in `Xeno.Desktop.Common`, built in code: extend `ColumnChart` (stacked and grouped, hover tooltip, click to select a bucket), and add `LineChart`, `DistributionChart`, `Heatmap`, `BarList`, `Meter`, `Sparkline`, and `KpiTile`. Each draws a `Xeno.Client.Core` chart model and nothing else. Copy chart as PNG to the clipboard.
- [ ] HM-6.14 Usage page with the shared structure: perspective, timeframe (including custom range), filters, group-by, compare, auto-refresh using the existing `RefreshSeconds` preference (`XenoFrontEndPreferences.cs:46`) as the default, export, KPI strip, chart tabs, data table toggle, breakdown and request tables with sort, column selection, and View JSON.
- [ ] HM-6.15 Overview page (`OverviewPage.cs:167`): token KPIs with deltas, a 24-hour sparkline, utilization meters; Models page: per-model tokens and p95 latency columns; Server page: the user's consumer and contributor totals.

### Terminal (`src/Xeno.Tui`)

- [ ] HM-6.16 Chart widgets that draw the shared chart models: extend `ChartView` with eighth-block stacked columns and shared boundary cells (the Mux technique), braille line charts with capacity and threshold lines, candlesticks in glyphs, a clock-aligned heatmap with the shared five-level ramp (`·` and colored blocks, `░▒▓█` under high contrast, `.-+*#` in ASCII), bar lists, meters, and sparklines. All respect `NO_COLOR`.
- [ ] HM-6.17 Usage page through `TuiPage.Frame` within 120x36: perspective and timeframe through `DropdownPicker`, custom range through a form, filters, group-by, compare, auto-refresh, KPI line, chart tabs (left and right arrows, Tab, and mouse clicks on tab labels), keys 1 to 4 for presets, bucket and cell selection with arrows and mouse clicks that filter the table below, a data table toggle, TSV copy and CSV export with `TuiContext.Notify` confirmation, and the chart explanation through `TuiContext.ShowMarkdownAsync`.
- [ ] HM-6.18 Overview, Models, and Server pages: the same additions as HM-6.15.

### Tray (`src/Xeno.Tray`)

- [ ] HM-6.19 Tooltip and menu summary: today's tokens served and consumed, ledger balance, utilization state with text label ("Utilization 62 percent, normal"), and the local endpoint route mix; menu items "Open usage" (desktop Usage page) and "Open usage in browser" (local dashboard). Refresh on the existing 5-second tray interval, reading the same `Xeno.Client.Core` view model.
- [ ] HM-6.20 Optional notification when utilization stays in the danger band for 5 minutes, off by default, configurable in `XenoSettingsCatalog`.

### CLI (`src/Xeno.Client/Program.cs`)

- [ ] HM-6.21 `xeno usage` with `--range`, `--from`/`--to`, `--perspective`, the filter flags, `--by <dimension>`, `--compare`, `--json`, `--csv`; prints the KPI block and a breakdown table. `xeno usage heatmap` prints the heatmap in glyphs; `xeno usage utilization` prints providers with meters and threshold labels. Add to help text.
- [ ] HM-6.22 `xeno status`: a one-line usage summary for the last 24 hours (served, endpoint, consumed).

### Tests

- [ ] HM-6.23 Web: Playwright cases for every usage component and page (each state, timeframe and custom range, filters reflected in the URL, auto-refresh pause while a modal is open, compare deltas, chart click-through narrowing the table, data table toggle, PNG, CSV, and JSON export, keyboard navigation of the heatmap, light and dark themes), in `dashboard/tests/`.
- [ ] HM-6.24 Front-end suites: every `XenoUsageCharts` chart from seeded DTOs, KPI delta direction semantics, desktop Usage page builds headlessly with every tab, tray summary text, parity manifest satisfied.
- [ ] HM-6.25 TUI suites through `TuiHarness`: Usage page renders at 120x36, every tab, preset keys, mouse clicks on tabs, buckets, and heatmap cells, table filtering from a click, `NO_COLOR` and ASCII glyph output, TSV copy and CSV export.
- [ ] HM-6.26 CLI cases: `xeno usage` text, JSON, and CSV output; heatmap and utilization subcommands; `xeno status` usage line.
- [ ] HM-6.27 i18n: keys for every usage label, KPI, tab, tooltip, threshold label, and empty state; number, duration, date, and percent formatters with explicit locale; pseudo-locale run for truncation in the KPI strip and tables.

### Docs

- [ ] HM-6.28 `README.md`: a Usage section with what each perspective shows and who can see it. `DESKTOP_AND_TERMINAL.md`: the Usage page, its keys, and the tray summary. Getting-started guides: where to find usage. `AGENTS.md`: the rule that usage charts come from `XenoUsageCharts` and the shared palette, and the parity manifest.
- [ ] HM-6.29 `CHANGELOG.md` under `## Unreleased`.

**Exit criteria:** the parity manifest has no missing entries; for one seeded data set, every surface shows identical KPI values, percentiles, heatmap levels, and series colors; every chart has a working data table, export, and empty state.

## Phase 7: Release readiness

- [ ] HM-7.1 Simulated user testing per `SIMULATED_USER_TESTING.md`, report at `user-testing/<date>-hybrid-models.md` with a coverage matrix across the CLI, server dashboard, local dashboard, desktop, terminal, tray, listener APIs, usage APIs, SDKs, and Postman. Journeys at minimum: an existing Ollama user upgrading with Ollama on 11434; a developer pointing the OpenAI Python SDK at the listener; a user adding Ollama Cloud as a local-only provider; a backend stopped mid-stream; the tunnel severed while local routes keep working; a machine at its concurrency limit serving both network and local requests; a contributor checking what their machine earned last week; a tenant member trying to see another member's usage; a tenant admin finding the busiest hour of the month from the heatmap and drilling into its requests; checking that `xeno status`, both dashboards, the desktop app, the terminal app, and the tray agree. Findings get a severity and wait for approval before fixes.
- [ ] HM-7.2 Run the Postman collection end to end against a real server and a real client with a real local Ollama.
- [ ] HM-7.3 Cross-front-end consistency pass against `DESKTOP_AND_TERMINAL.md` and the usage parity manifest.
- [ ] HM-7.4 Load check of the usage APIs with a year of seeded rollups on each database driver: record p95 query time for a 12-month custom range timeseries and a Last month heatmap in the log; anything over 500 ms gets an index or query fix before Phase 8.

**Exit criteria:** no open S1 or S2 findings.

## Phase 8: UI, aesthetics, and UX examination

The last phase looks at everything this plan built as one product, with fresh eyes, on every surface. Phases 1 through 7 each check their own work. This phase catches what only shows up side by side: a timeframe control that behaves differently on two pages, a tooltip that rounds where the table does not, a heatmap that reads as green noise in dark mode, an auto-refresh that yanks a table out from under a user's scroll position.

Do the examination with real data (the Phase 7 seeded year plus a live client), on the web at 1280, 768, and 390 px in light and dark themes, in the desktop app on macOS and Windows in light, dark, and high contrast, and in the terminal app at 120x36 and larger with color, `NO_COLOR`, and ASCII. Record every finding in `user-testing/<date>-hybrid-models-ux.md` with a screenshot or terminal capture, the surface, a severity, and a proposed fix. Nothing is fixed until the product owner approves the list.

### Charts and heatmaps

- [ ] HM-8.1 Each chart answers one question, says so in its title, and has a unit on its axis or in its tooltip. Remove any chart nobody can name a question for.
- [ ] HM-8.2 Axes: nice ticks (1, 2, 5 times a power of ten), no overlapping labels at any width, time labels in the viewer's zone with the zone shown, gridlines quieter than data.
- [ ] HM-8.3 Stacked charts: series order is the same everywhere (prompt below completion, success below failure, local below network below fallback); totals are visible on hover; small series are still hoverable.
- [ ] HM-8.4 Candlesticks: the legend explains wick, box, average line, and p99 tick once per page; the approximation note on percentiles is present and not alarming.
- [ ] HM-8.5 Heatmaps: the five levels are distinguishable in light, dark, and high contrast, and for the common color vision deficiencies (check with a simulator); the legend reads Less to More with the peak value; empty cells are visibly different from zero-but-active cells; row and column labels align with the clock; cell size stays square-ish at every width.
- [ ] HM-8.6 Utilization: capacity and threshold lines are labeled, threshold text appears with color, gaps are gaps and not drops to zero, a reset or disconnect is marked.
- [ ] HM-8.7 Empty, sparse, and huge data: a brand new account, a single request, one model, fifty models (the `(other)` row), a year of data, and a window that exceeds raw retention (coverage warning) all look intentional.
- [ ] HM-8.8 Tooltips: same content, order, rounding, and number formats as the data table and the KPI strip for the same bucket; never clipped by the viewport; keyboard reachable.

### Controls

- [ ] HM-8.9 Timeframe selector: same presets, order, labels, default, and keyboard behavior on every page and surface; custom range validates (start before end, not in the future, not beyond retention) with clear messages; switching range keeps filters and tab.
- [ ] HM-8.10 Auto-refresh: same choices and default everywhere; the "Updated" indicator is visible but quiet; refresh never resets scroll, selection, open tooltips, sort, or the table page; pauses while a modal is open, a cell is selected, or the window is hidden, and says so.
- [ ] HM-8.11 Filters and group-by: active filters are visible as chips that can be removed one at a time; "clear all" exists; URL round-trips on the web; impossible combinations are prevented, not answered with an empty chart.
- [ ] HM-8.12 Compare-to-previous: deltas use the same rounding as the values; direction colors match meaning, not sign; a zero previous value reads "new", not "infinity".
- [ ] HM-8.13 Export: PNG output includes title, range, and legend; CSV headers are human-readable and match the table; file names include the view and range.
- [ ] HM-8.14 Chart click-through: the selected bucket is highlighted, the table chip names the slice, and clearing the chip restores the table without a reload.

### Tables

- [ ] HM-8.15 Every supporting table: numeric columns right-aligned with consistent precision, units in headers, sort indicators, pagination above the table, column selection persisted, row actions consistent with the rest of the dashboard, no horizontal scroll at 1280 px with default columns, sensible collapse at 390 px.
- [ ] HM-8.16 Table and chart agree: totals in the breakdown table equal the chart for the same window, including `(other)`.

### Color, type, and layout

- [ ] HM-8.17 Palette: semantic tones are only used for meaning (success, danger, warning), never as series colors; series colors are stable across a view; contrast meets WCAG AA for text and 3:1 for chart marks against the background in both themes; dark theme gets its own strong hues (HM-5.24).
- [ ] HM-8.18 Typography and spacing: KPI values, labels, and deltas use the same scale on every page; chart titles and table headers are consistent; spacing follows the dashboard tokens; nothing is cramped at 768 px.
- [ ] HM-8.19 Page layout: usage, local endpoint, ledger, leaderboard, telemetry, and home pages share one header-controls pattern, one KPI strip, and one chart card style.
- [ ] HM-8.20 Desktop: native feel on macOS and Windows; charts redraw cleanly on resize and theme change; menus and shortcuts for usage actions exist in `XenoMenuLayout`.
- [ ] HM-8.21 Terminal: every usage and local endpoint view fits 120x36 without truncating values; focus is always visible; mouse and keyboard reach every control; glyph charts read correctly in the default macOS Terminal, iTerm2, Windows Terminal, and a Linux terminal.
- [ ] HM-8.22 Tray: summary text is short enough not to be truncated on macOS and Windows; numbers match the desktop Overview.

### Usability and accessibility

- [ ] HM-8.23 Screen reader pass on the web usage page and local endpoint page: chart summaries are announced, heatmap navigation reads cell and value, KPIs read value and delta in words.
- [ ] HM-8.24 Keyboard-only pass on every new page in every surface.
- [ ] HM-8.25 First-run pass: a new contributor and a new consumer can each answer "how much did I use or serve this week, and on which models?" in under a minute without help.
- [ ] HM-8.26 Copy pass: labels, empty states, coverage warnings, threshold labels, fallback explanations, and error messages are plain, consistent, and translated.

### Close

- [ ] HM-8.27 Fix approved findings, rerun the affected tests on all three .NET hosts and the dashboard suite, and update screenshots in the docs if any exist.
- [ ] HM-8.28 Final read of every touched doc, Postman description, and comment for accuracy and em-dashes.
- [ ] HM-8.30 Final version sweep: no `0.1.x` or `v0.1.x` remains anywhere outside `archive/` and older CHANGELOG sections (code, docs, Docker, Postman, scripts, installers, dashboard, SDKs, tests); every image reference is `v0.2.0`; the CHANGELOG heading drops "in progress".
- [ ] HM-8.29 `CHANGELOG.md` `## Unreleased` reviewed as a whole. The version is already 0.2.0 by product owner request (Phase 0); propose any further change rather than applying it.

**Exit criteria:** every approved finding is fixed or explicitly deferred with a reason; the product owner signs off.

## Requirements coverage

The `~/Code/Agents/requirements` documents are written for server products with databases and tenants. The table records how each applies to a loopback client feature, so a reviewer can see what was considered rather than guess at what was skipped.

| Requirement source | How this plan handles it |
|---|---|
| CODE_STYLE | One type per file, no `var`, no tuples (the router returns `LocalInferenceRouteDecision`), XML docs with defaults and ranges, `CancellationToken` and `ConfigureAwait(false)` throughout, specific exception types, no `Console.WriteLine` in library code (HM-2.13). |
| BACKEND_ARCHITECTURE: Watson 7, loopback | The listener stays a Watson 7 service bound to `127.0.0.1`. Local provider defaults use `127.0.0.1`. |
| BACKEND_ARCHITECTURE: typed DTOs, JSON rule | Every translated body and chunk is a named DTO (HM-2.12, HM-4.1). Passthrough bodies are forwarded as bytes and never inspected. |
| BACKEND_ARCHITECTURE: settings API | Restart-required flags, per-field validation, secrets reported as set or not set (HM-1.13, HM-1.15, HM-3.7). |
| BACKEND_ARCHITECTURE: request history | Metadata-only local request log (HM-3.4). Bodies stay off because `AGENTS.md` forbids prompt logging without a diagnostic setting, and the stricter rule wins. Usage summaries come from rollups, not from loading raw rows (HM-5.8). |
| BACKEND_ARCHITECTURE: OpenAPI on every route | The listener's existing `/openapi.json` gains the route headers. New management routes go on the dashboard API, since `AGENTS.md` fixes the listener's route list. |
| BACKEND_ARCHITECTURE: database, PrettyId, tenancy | Routing: not applicable, because the client persists routing to `client.json`, has one local user, and keys route rules by model name. Usage: applies in full. Usage columns and rollup tables are structured columns, never JSON blobs. Migrations, backfill, retention, and the four-driver matrix go through `DatabaseDriverFactory` (HM-5.1 to HM-5.4, HM-5.28), and tenant isolation applies to every usage query (HM-5.15). Rollup rows are aggregates keyed by their dimensions, so they need no PrettyId. |
| AUTHENTICATION: usage permissions | New `Usage` permission seeded per built-in role, perspective enforcement, standard 403 body (HM-5.14, HM-5.15). |
| AUTHENTICATION: secret handling | Provider credentials are never logged or returned, and are masked in every front end. Encryption at rest of `client.json` credentials is an existing gap, tracked separately by product owner decision (2026-11-09). |
| TELEMETRY | Route outcome counters and per-provider latency without model-name labels or secrets (HM-3.8). Rollup queue drops and utilization gauges on the server (HM-5.37). The compose observability stack does not apply to a desktop client. The server's Grafana dashboards remain the operator view, and the usage pages are the user view. |
| DASHBOARD_STYLE_AND_USABILITY | Tables, row actions, states, modals, API Explorer, and visual QA (HM-3.16 to HM-3.18, HM-3.23). Usage view structure, auto-refresh pause while a modal is open, and copy-to-clipboard export (Phase 6). Cross-surface UX examination (Phase 8). |
| FRONTEND_ARCHITECTURE, I18N | Shared API client, no chart library, i18n keys, backend reason codes translated in the UI, and locale-explicit formatters for usage numbers, durations, and dates (HM-1.21, HM-3.22, HM-6.27). |
| BACKEND_TEST_ARCHITECTURE | All cases in `src/Test.Shared`, run by `Test.Automated`, `Test.Xunit`, and `Test.Nunit`. Fakes bind to `127.0.0.1`. The repo uses `Test.Xunit` rather than the documented `Test.Xunit3`, which predates this plan. |
| REPOSITORY_REQUIREMENTS: REST_API.md, Postman | HM-3.31, HM-3.32. |
| REPOSITORY_REQUIREMENTS: CI workflow, AOT | Added in Phase 1 (HM-1.31 to HM-1.33). |
| SIMULATED_USER_TESTING | HM-7.1 (functional) and HM-8 (UX examination). |
| BENCHMARKING | The full router overhead benchmark is deferred. It compares the listener with calling Ollama directly, and is worth doing once Phase 2 is stable, but it should not block the feature (see "Later"). The usage query load check runs in HM-7.4. |
| INSTALLERS, scripts | No script, installer, or Docker file references the listener port today. Verify during HM-1.26 and update `scripts/README.md` only if that changes. |
| EXAMPLE_APPLICATIONS | No example apps exist in the repo. Not applicable. |
| SDKs (`src/sdk/*`) | Routing: no SDK change, because the Xeno SDKs target the server and apps reach the listener with the stock OpenAI and Ollama SDKs (HM-2.25). Usage: each SDK gets typed methods for every server usage route, with tests in its harness against `127.0.0.1` (HM-5.38). |
| VERSIONING | 0.2.0 (alpha, image tag `v0.2.0`) by explicit product owner request (Phase 0, HM-8.30). No other version changes. |
| WRITING_DOCUMENTS | No em-dashes in any doc, Postman description, or comment touched by this plan (HM-8.28). |

## Resolved questions

Answered by the product owner on 2026-11-09.

| # | Question | Answer |
|---|---|---|
| Q1 | Listener port | 11435. |
| Q2 | Default concurrency limit | 4, configurable globally, per provider, and per model (HM-1.4, HM-3.1). |
| Q3 | Credentials at rest | Tracked separately, outside this plan. |
| Q4 | CI | Added in Phase 1 (HM-1.31 to HM-1.33). |
| Q6 | Rollup granularity and retention | Follow Mux and Armada: Last hour 120 x 30 s, Last day 96 x 15 min, Last week 84 x 2 h, Last month 90 x 8 h (see "One windowing rule"). |
| Q7 | Viewer usage scope | Tenant-wide, matching the role's existing read permission. |
| Q8 | Public leaderboard | Global stays public; per-tenant leaderboards are opt-in (HM-5.16). |
| Q9 | Time zone | Stored in UTC, rendered in the viewer's local zone. |
| Q5 | Busy worker behavior | Answered from the code: the server excludes clients at their stored `MaxParallelRequests` and returns 502 when none are left, with no retry. HM-3.3 now covers reporting real capacity and a single retry. |

## Later

SharpAI fits as another provider type once this plan lands, deployed separately like Ollama and reached the same way. Two details to remember when that work starts: SharpAI defaults to port 8000, which is also vLLM's default, and it always requires a bearer token, so it should wait for the separately tracked work on storing credentials securely.

Other deferred ideas: glob or prefix route rules, load balancing across several local providers serving the same model, mid-stream failover, owner-reserved capacity on a shared provider, the router overhead benchmark, cost in currency (usage is in tokens and ledger units today), cached-token reporting once providers expose it, and usage alerts beyond the tray utilization notice.

## Progress log

| Date | Item | Who | Note |
|---|---|---|---|
| 2026-11-09 | Plan | | Drafted from the product owner brainstorm. |
| 2026-11-09 | HM-1.11, HM-1.12 | | `ClientRunnerSettings.Endpoint`, `XenoForms` provider endpoint, and `SyntheticClientFactory` use 11434 for a real Ollama provider, not the listener. Left unchanged. |
| 2026-11-09 | HM-1.20 | | The tray status line and tooltip now show the local endpoint address. A "Copy local endpoint URL" menu item needs a new shared command and a clipboard owner the tray lacks; it moves to HM-3.10 with the other Local endpoint commands. |
| 2026-11-09 | HM-1.4 | | The CLI sets provider and model limits with `--max-concurrent`. The global default is set in the settings catalog (desktop, terminal), the local dashboard, or `client.json`; a CLI command for it lands with `xeno inference` in HM-3.15. |
| 2026-11-09 | HM-1.17 | | The local dashboard settings form rebuilt `localInferenceServer` from the fields it rendered, which would have wiped `Routes` on every save. It now spreads the existing camelCase fields first. Covered by the new Playwright case. |
| 2026-11-09 | HM-1.33 | | PostgreSQL and MySQL pass 231/231 locally once their connection limits are raised (PostgreSQL 500, MySQL 1000); the workflow raises them. SQL Server was not run locally (no image on this machine). The workflow has not run on GitHub yet because nothing has been pushed. |
| 2026-11-09 | HM-1.34 | | Both cases were stale tests, not product bugs: the NetLedger link moved to Settings > Related Consoles, and the credential form now sends the member's own user ID, which the server allows (`SelfServiceCredentialOnly` blocks anyone else). |
| 2026-11-09 | Phase 1 | | Build clean; Test.Automated, Test.Xunit, Test.Nunit 231/231; both Playwright suites pass (2 local, 28 server); i18n check passes. Pre-existing em-dashes remain in README.md and the getting-started client guides; HM-8.28 removes them. |
| 2026-11-09 | HM-2.12 | | The telemetry DTOs in `Client.Core/Telemetry/Models` carry only the fields telemetry needs, so re-serializing through them would drop `model`, `digest`, `modified_at`, and Ollama Cloud's `remote_model`/`remote_host`. Listing DTOs live in `Xeno.Client.Core/Routing` instead (`LocalOpenAiModelList`, `LocalOllamaTagsResponse`, `LocalOllamaPsResponse`, and their items). Timestamps stay strings because Ollama sends nanosecond precision. |
| 2026-11-09 | HM-2.13 | | `Xeno.Client` is the CLI and worker executable, not a library, so its console lines stay. Routing logs model, path, provider, and reason only; `LocalRoutingNeverLogsPromptContent` checks both local and network paths with a sentinel prompt. |
| 2026-11-09 | HM-2.14 | | One scriptable `FakeProviderServer` (HttpListener) stands in for Ollama, vLLM, and OpenAI-compatible providers. `HttpListener.Abort` ends a chunked body cleanly on macOS, so `TruncatedStreamProviderServer` (raw socket) simulates a provider that dies mid-stream. |
| 2026-11-09 | HM-2.11 | | Front ends call `ModelCatalogBuilder.Merge` without health snapshots today, so discovered models only appear once Phase 3 gives the workspace its own provider checks (HM-3.6). Route targets for configured models are already correct. |
| 2026-11-09 | Phase 2 | | Found and fixed while testing: when health checks already showed a rule's provider was down, the router chose the network without marking it a fallback, so `X-Xeno-Fallback` and `ExcludeRequestingClient` were missing. `LocalInferenceRouteDecision.IsFallback` now carries it. |
| 2026-11-09 | Phase 2 | | Build clean; Test.Automated, Test.Xunit, Test.Nunit 265/265. The exit check against a real local Ollama is not done: no Ollama runs on this machine. `NetworkOnly` passthrough is covered by the existing tunnel tests, with `LocalInferenceListenerProxiesTypedResponsesAndHeaders` pinned to `NetworkOnly`. |
| 2026-11-09 | HM-3.3 | | Answer to Q5 implemented: heartbeats carry `networkCapacity` (shared provider limits minus local in-flight), stored through a new `IClientMethods.UpdateCapacityAsync` in all four drivers. A full provider answers with `ClientResponseMessage.Busy`; `RelayWithBusyRetryAsync` re-routes once excluding that client, then returns `503 AllClientsBusy` with `Retry-After`. The local-inference relay path (`RelayClientLocalInferenceRequestAsync`) does not retry yet. |
| 2026-11-09 | HM-3.4, HM-3.5 | | The listener runs inside the worker process, but the CLI, desktop, terminal, and tray run in other processes, so live data is file-backed: `~/.xeno/local-inference-requests.jsonl` (metadata-only log) and `~/.xeno/local-inference-status.json` (health, concurrency, counters; written at most once a second, removed on stop). Token counts come from complete responses only; streamed token counts arrive with the Phase 5 rollups. |
| 2026-11-09 | HM-3.8 | | The client has no OpenTelemetry meters of its own (`ClientProviderTelemetryService` samples provider metrics, it does not count requests), so route outcome counters live in `LocalInferenceRuntimeStatus` and the request log. Phase 5 turns them into usage rollups. |
| 2026-11-09 | HM-3.10 | | Only `GoLocalEndpoint` became a shared command (navigation, palette, tray). Add, edit, delete, test, and check are page actions on the Local endpoint page in each app, the same pattern the Providers page uses, instead of global commands that would be meaningless on other pages. The tray's copy-URL item became Open Local Endpoint, which launches the desktop app on that page (`--page localendpoint`); the desktop page has Copy URL. |
| 2026-11-09 | HM-3.22 | | The desktop and terminal apps are English-only today (strings live in the shared presentation layer); this plan follows that pattern. The web dashboard strings are in `resources.js` for English and Spanish. |
| 2026-11-09 | HM-3.23 | | Screenshots at 1280, 768, and 390 px in light and dark: no horizontal page overflow. Fixed from the review: the endpoint URL rendered uppercase inside the KPI card, request times showed raw ISO strings, empty provider cells got a copy button, and "answered here" and "sent to the network" were both green-teal. Network traffic now uses the info tone. |
| 2026-11-09 | HM-3.28 | | Shared presentation, routing, forms, chart, tray, and navigation cases run on all three hosts. There is no headless Avalonia harness in the repo, so the desktop page is only compiled, not rendered, in tests. Tracked for Phase 8. |
| 2026-11-09 | Phase 3 | | Build clean; Test.Automated, Test.Xunit, Test.Nunit 289/289; local dashboard Playwright 4/4; server dashboard Playwright 28/28; i18n check passes. |
| 2026-11-09 | HM-1.33 | | SQL Server 2022 in an ephemeral container: 290/290. The SQL Server image creates no database, so the maintenance database must be `master`; the workflow now sets `XENO_TEST_DB_NAME` per provider. |
| 2026-11-09 | Phase 2 exit | | End to end against a real Ollama gateway (per-model base URLs with a bearer token), listener on a scratch client home, no Xeno tunnel. Passed: merged `/v1/models` and `/api/tags` (52 models, including `:cloud`), `/api/chat` with `qwen3.8:27b` (non-streaming and NDJSON streaming), `/v1/chat/completions` streaming with `gpt-oss:20b` (SSE through `[DONE]`), `/api/show`, `/api/ps`, the provider bearer replacing the caller's dummy `Authorization`, a network-only model returning a provider-shaped 502 with no tunnel, the shared limit (second request 429 with `Retry-After` after the 2 s queue while a 29 s stream finished), the metadata-only request log, the status file, and `xeno inference backends/requests/resolve`. |
| 2026-11-09 | Phase 2 | | Found against the real gateway and fixed: each base URL serves one model but its `/api/tags` lists every model, so discovery made the first provider claim models configured on another, and requests got 401s. The router and the listings now prefer providers that have the model configured over providers that only reported it (`RouterPrefersConfiguredOverDiscovered`). Behind a gateway like this, set `DiscoverLocalModels` to false; discovered `:cloud` models otherwise route to a runner that refuses them. Also fixed: the request log listed by completion time instead of arrival time. Noted for Phase 8: listener error JSON escapes apostrophes as `\u0027`. |
| 2026-11-09 | HM-1.31 | | First GitHub Actions runs (the repo had no CI before this plan). Green: Full suite (ubuntu-latest), Provider matrix (postgresql), Provider matrix (sqlserver). Fixed along the way: dashboards must build before .NET (the client embeds dist-local), SQL Server needs `master` as the maintenance database, the Docker test needs Linux containers, the locator test assumed a Unix PATH, the sign-in test did not really wait, and the server rewrote the whole client record after every relay (stale in-flight count, clobbered reported capacity). Still failing, all in code paths this plan did not change and that never ran on these runners before: (1) `RunRelayStreamingBurstAsync` intermittently gets 502 `NoEligibleClients` on macOS and Windows: the server decrements a client's in-flight count only after its response bookkeeping finishes, so ten back-to-back requests on a slow runner can find four slots still held; (2) `Concurrent least-recently-used gpt-oss requests spread across eligible clients` fails on MySQL in CI only ("Got timeout reading communication packets", 13.7 s on the runner versus 1.6 s locally, where it passes); (3) `Client connect starts background worker and logs` times out on Windows. HM-1.33 stays open for these. |
| 2026-11-09 | HM-1.33 | | Fixes for the remaining CI failures. Relay: a client's in-flight slot is now released (`ClientInFlightSlot`) as soon as its final response arrives, before history, ledger, and the final chunk; the burst test reports client state if it fails again. MySQL: the concurrent LRU test blocked 64 thread-pool threads (`ManualResetEventSlim.Wait` plus `GetResult`), starving the driver's async I/O; it is now fully async. Windows: `client.json` and `state.json` reads did not share delete access, so a writer's atomic replace failed while any reader had the file open; reads and writes now go through `ClientFileIo` (shared reads, atomic replace, brief retries) and `ClientSettings` loads are serialized in-process; the terminal app retries a failed first status read; the background-start test allows 30 s. The request-log test waits for the record written in post-routing. Local suite 290/290. |
| 2026-11-09 | HM-0.7 | | Build clean; Test.Automated, Test.Xunit, Test.Nunit 214/214; C#, JavaScript, Python SDK tests pass; i18n check passes; local dashboard Playwright passes. Two server dashboard Playwright cases already fail on `main` (`ledger page renders system admin controls and summaries`: no "NetLedger UI" link; `regular tenant members see member dashboard surfaces`: credential POST sends `userId`). Not caused by the bump; fix tracked as HM-1.34. |
| 2026-11-09 | Q1 to Q9 | | Product owner answered Q1 to Q4 and Q6 to Q9; Q5 answered from `RoutingService.cs`. Plan updated. |
| 2026-11-09 | Phases 5, 6, 8 | | Added usage visualization (from Mux and Armada surveys) and the final UX examination at the product owner's request. |

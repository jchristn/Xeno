# Additional Xeno Client Provider Telemetry Plan

Status: implemented for Xeno Client local runtime, local dashboard, local SQLite persistence, local dashboard API, documentation, Postman, and automated tests.

This file now reflects what was actually built. Items that were intentionally not implemented are listed under Deferred Work.

## Implemented

### Client Settings

- [x] Added `ClientProviderTelemetrySettings`.
- [x] Added `ClientProviderTelemetryEndpointOverride`.
- [x] Added `Telemetry.ProviderTelemetry` to `ClientTelemetrySettings`.
- [x] Provider telemetry is enabled by default.
- [x] Provider telemetry collection interval defaults to `15000` ms.
- [x] Provider telemetry collection interval is clamped to `>= 5000` ms.
- [x] Provider telemetry request timeout defaults to `5000` ms.
- [x] Provider telemetry request timeout is clamped from `500` to `300000` ms.
- [x] Concurrent provider limit defaults to `4`.
- [x] Concurrent provider limit is clamped from `1` to `16`.
- [x] Metrics endpoint discovery is enabled by default.
- [x] Raw metrics persistence is disabled by default.
- [x] Raw metrics max bytes is clamped from `0` to `1048576`.
- [x] Endpoint overrides include provider name, enabled state, provider kind, metrics URL, health URL, and models URL.
- [x] Local dashboard settings UI exposes provider telemetry settings.
- [x] Local dashboard settings UI clearly labels provider telemetry changes as restart-sensitive in this alpha build.

### Typed Contracts

- [x] Added provider telemetry enums:
  - `ClientProviderTelemetryKindEnum`
  - `ClientProviderTelemetryStatusEnum`
  - `ClientProviderTelemetryHealthStatusEnum`
  - `ClientProviderTelemetryMetricEnum`
  - `ClientProviderTelemetryMetricSourceEnum`
- [x] Added provider telemetry snapshot/status contracts:
  - `ClientProviderTelemetrySnapshot`
  - `ClientProviderTelemetryMetadata`
  - `ClientProviderTelemetryProviderStatus`
  - `ClientProviderTelemetryEndpoint`
  - `ClientProviderTelemetryModel`
  - `ClientProviderTelemetryMetric`
  - `ClientProviderTelemetryLabel`
- [x] Added provider telemetry persistence/API contracts:
  - `ClientProviderTelemetrySampleRecord`
  - `ClientProviderTelemetryModelSampleRecord`
  - `ClientProviderTelemetryMetricSampleRecord`
  - `ClientProviderTelemetrySampleDetail`
  - `ClientProviderTelemetryLatestResponse`
  - `ClientProviderTelemetrySearchRequest`
  - `ClientProviderTelemetrySearchResult`
  - `ClientProviderTelemetryRollupRequest`
  - `ClientProviderTelemetryRollupBucket`
- [x] Added provider response contracts:
  - `OllamaVersionResponse`
  - `OllamaTagsResponse`
  - `OllamaTagModelResponse`
  - `OllamaModelDetailsResponse`
  - `OllamaPsResponse`
  - `OllamaLoadedModelResponse`
  - `OpenAiModelsResponse`
  - `OpenAiModelResponse`
  - `PrometheusMetricDocument`
  - `PrometheusMetricSample`
- [x] Contracts use named classes.
- [x] New provider telemetry code does not use `JsonElement`, `JsonNode`, `JsonObject`, or `JsonArray`.
- [x] New provider telemetry code does not use `var`.
- [x] New provider telemetry code avoids tuples.
- [x] Public C# types and members include XML documentation.

### Collection Runtime

- [x] Added `IClientProviderTelemetryService`.
- [x] Added `IPrometheusMetricsParser`.
- [x] Added `PrometheusMetricsParser`.
- [x] Added `ClientProviderTelemetryService`.
- [x] Added `ClientProviderTelemetryPersistenceService`.
- [x] Provider telemetry starts with the background Xeno Client runtime.
- [x] Provider telemetry collection runs independently of request handling.
- [x] Provider telemetry supports immediate collect-now behavior.
- [x] Provider telemetry keeps `LatestSnapshot` and runtime status.
- [x] Provider telemetry shuts down with the local dashboard runtime.
- [x] Provider telemetry pruning uses the same retention window as local telemetry.
- [x] Provider telemetry uses conservative HTTP timeouts and per-provider failure isolation.
- [x] Provider telemetry supports endpoint overrides.
- [x] Provider telemetry supports best-effort process correlation when enabled.

### Provider Coverage

- [x] Ollama collection uses `/api/version`.
- [x] Ollama collection uses `/api/tags` for available model inventory.
- [x] Ollama collection uses `/api/ps` for loaded model inventory when available.
- [x] Generic OpenAI-compatible collection uses `/v1/models`.
- [x] vLLM-style telemetry uses `/metrics` Prometheus scraping when exposed.
- [x] OpenAI-compatible Prometheus endpoints are parsed and normalized when exposed.
- [x] LocalAI, llama.cpp, LM Studio, and TGI are handled through OpenAI-compatible model discovery plus `/metrics` when exposed.
- [x] OpenAI and Gemini cloud providers degrade gracefully to limited reachability/model metadata where available.
- [x] Raw Prometheus metrics are only persisted when explicitly enabled and bounded by `RawMetricsMaxBytes`.
- [x] Prompts, responses, provider credentials, and authorization headers are not collected as provider telemetry.

### Prometheus Parsing And Normalization

- [x] Parser accepts Prometheus text exposition format.
- [x] Parser ignores comments and invalid lines.
- [x] Parser parses metric name, labels, numeric value, and optional timestamp.
- [x] Parser represents labels with named `ClientProviderTelemetryLabel` objects.
- [x] Parser avoids forbidden STJ DOM types.
- [x] Parser has automated coverage for label parsing.
- [x] Normalizes vLLM-style active/running request metrics.
- [x] Normalizes vLLM-style waiting/queued request metrics.
- [x] Normalizes vLLM-style swapped request metrics.
- [x] Normalizes vLLM-style cache metrics.
- [x] Normalizes vLLM-style prompt and generation token counters.
- [x] Normalizes vLLM-style request/error counters.
- [x] Normalizes vLLM-style latency and time-to-first-token histogram buckets.

### Local SQLite Persistence

- [x] Added `IClientProviderTelemetryHistoryMethods`.
- [x] Added `SqliteClientProviderTelemetryHistoryMethods`.
- [x] Added `LocalDashboardDatabaseDriverBase.ProviderTelemetryHistory`.
- [x] Added `SqliteLocalDashboardDatabaseDriver.ProviderTelemetryHistory`.
- [x] Added startup schema migration for provider telemetry tables.
- [x] Added idempotent migration support for process-correlation columns.
- [x] Added `local_provider_telemetry_samples`.
- [x] Added `local_provider_telemetry_models`.
- [x] Added `local_provider_telemetry_metrics`.
- [x] Added indexes for provider/time, kind/time, status/time, endpoint/time, model lookup, and metric lookup.
- [x] Persisted sample fields include provider, API type, kind, endpoint hash/display, timestamps, status, health, version, counts, queues, tokens, latency, cache, memory, process correlation, raw metrics metadata, snapshot JSON, and error.
- [x] Persisted model fields include provider, model, family, quantization, parameter size, context length, loaded state, size, memory, and expiry.
- [x] Persisted metric fields include provider, model, metric name, kind, value, unit, source, and labels JSON.
- [x] SQL uses parameters for user-supplied values.
- [x] Deletes remove child rows through foreign-key cascade.
- [x] Search supports paging, provider, model, status, metric, and time range.
- [x] Rollups emit empty buckets for selected ranges.
- [x] Retention pruning removes old provider telemetry.

### Request History Correlation

- [x] Added nullable local request history columns:
  - `provider_telemetry_sample_id`
  - `provider_telemetry_health_status`
  - `provider_telemetry_queued_requests`
- [x] Added idempotent startup migration for these columns.
- [x] Local request history links the latest provider telemetry sample for the selected provider when available.
- [x] Local request details include correlated provider telemetry fields.

### Local Dashboard API

- [x] Added `GET /api/v1/telemetry/providers/status`.
- [x] Added `GET /api/v1/telemetry/providers/latest`.
- [x] Added `POST /api/v1/telemetry/providers/collect`.
- [x] Added `POST /api/v1/telemetry/providers/history/search`.
- [x] Added `POST /api/v1/telemetry/providers/history/rollups`.
- [x] Added `GET /api/v1/telemetry/providers/history/{id}`.
- [x] Added `DELETE /api/v1/telemetry/providers/history/{id}`.
- [x] Added `POST /api/v1/telemetry/providers/history/delete`.
- [x] Routes reuse local dashboard token authorization.
- [x] Routes use named request/response contracts.
- [x] Routes support standard local dashboard range defaults.
- [x] Routes support provider/model filters and metric selection.

### Local Dashboard UX

- [x] Added `Provider Telemetry` page.
- [x] Added `Provider Telemetry Analytics` page.
- [x] Added navigation entries under the local dashboard administration section.
- [x] Added provider telemetry KPI cards.
- [x] Added full-width provider telemetry charts.
- [x] Added provider/model/metric filters.
- [x] Added provider telemetry latest/status tables.
- [x] Added provider telemetry sample history table.
- [x] Added provider telemetry detail modal.
- [x] Added provider telemetry settings controls.
- [x] Added copy-to-clipboard support through existing global copy controls.
- [x] Added descriptive tooltips and i18n strings for new controls.
- [x] Provider telemetry pages use the same card, table, chart, modal, dark/light mode, and control patterns as the existing local dashboard.
- [x] Charts use the existing shared bucket rules and auto-refresh behavior.
- [x] Tables use the existing pagination, filtering, refresh, row-click, View JSON, and action menu behavior.

### Server Protocol Preparation

- [x] `WEBSOCKETS_API.md` documents reserved provider telemetry frame names:
  - `server.providerTelemetry.request`
  - `client.providerTelemetry.snapshot`
  - `client.providerTelemetry.history`
  - `client.providerTelemetry.error`
  - `server.providerTelemetry.ack`
- [x] Documentation states that reserved provider telemetry frames must not include credentials, prompts, responses, authorization headers, or unbounded raw metrics.
- [x] Runtime server-requested provider telemetry is intentionally deferred.

### Documentation

- [x] Updated `README.md`.
- [x] Updated `CLIENT_GETTING_STARTED.md`.
- [x] Updated `REST_API.md`.
- [x] Updated `WEBSOCKETS_API.md`.
- [x] Updated `CHANGELOG.md`.
- [x] Updated C# SDK README scope note.
- [x] Updated JavaScript SDK README scope note.
- [x] Updated Python SDK README scope note.
- [x] Documentation states provider telemetry is best-effort.
- [x] Documentation states cloud providers expose limited telemetry.
- [x] Documentation states raw metrics persistence is optional and bounded.
- [x] Documentation states provider telemetry does not collect prompts, responses, or credentials.
- [x] Documentation states provider telemetry retention follows local telemetry retention.

### Postman

- [x] Added `localProviderTelemetrySampleId` variable.
- [x] Added provider telemetry status request.
- [x] Added latest provider telemetry request.
- [x] Added collect provider telemetry request.
- [x] Added provider telemetry search request.
- [x] Added provider telemetry rollups request.
- [x] Added provider telemetry detail request.
- [x] Added delete provider telemetry detail request.
- [x] Added delete provider telemetry range request.
- [x] Included local dashboard token header.
- [x] Included sample request bodies for search and rollups.

### SDKs

- [x] Reviewed C# SDK.
- [x] Reviewed JavaScript SDK.
- [x] Reviewed Python SDK.
- [x] Confirmed SDKs target the Xeno server API.
- [x] Documented that provider telemetry is exposed through the client-local dashboard API, not wrapped by the server SDKs.
- [x] No SDK methods were added because no server API surface was added for provider telemetry.

### Tests

- [x] Added Touchstone-backed settings/defaults/clamp coverage.
- [x] Added PrettyId shape coverage for provider telemetry IDs.
- [x] Added Prometheus parser coverage.
- [x] Added SQLite provider telemetry persistence coverage.
- [x] Added SQLite provider telemetry rollup coverage.
- [x] Added local dashboard provider telemetry API route coverage.
- [x] Added local dashboard token-path coverage through the existing local dashboard API test.
- [x] Existing xUnit and NUnit runners both execute the shared provider telemetry tests.

### Usability And Design Passes

- [x] Pass 1: information architecture and workflow reviewed against the existing local dashboard and server dashboard patterns.
- [x] Pass 2: visual/layout consistency reviewed for navigation, cards, charts, tables, filters, modals, and settings controls.
- [x] Pass 3: operational states reviewed for no-data, unavailable provider, disabled provider, token failure, raw metrics, copy controls, row click, and filtering behavior.

### Validation Completed

- [x] `dotnet build src\Xeno.Client\Xeno.Client.csproj`
- [x] `dotnet build src\Xeno.sln`
- [x] `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-build`
- [x] `dotnet test src\Test.Nunit\Test.Nunit.csproj --no-build`
- [x] `dotnet test src\sdk\csharp\Xeno.Sdk.Tests\Xeno.Sdk.Tests.csproj --no-build`
- [x] `npm.cmd test`
- [x] `npm.cmd run build`
- [x] `npm.cmd run build:local`
- [x] Postman JSON validation.
- [x] `git diff --check`.
- [x] Targeted scan for `var`, `JsonElement`, `JsonNode`, `JsonObject`, and `JsonArray` in new provider telemetry code.
- [x] Confirmed local Ollama at `http://127.0.0.1:11434/api/version` was reachable during validation.

## Deferred Work

These items are intentionally not complete in this pass.

- [ ] Implement runtime server-requested provider telemetry websocket handling.
- [ ] Implement proactive client-to-server provider telemetry upload.
- [ ] Add server-side provider telemetry storage across SQLite, PostgreSQL, SQL Server, and MySQL.
- [ ] Add server dashboard provider telemetry pages after the server receives provider telemetry.
- [ ] Add server SDK provider telemetry methods after a server API exists.
- [ ] Add local dashboard SDKs if the product later exposes a stable local-dashboard SDK surface.
- [ ] Live-validate vLLM telemetry against a running vLLM `/metrics` endpoint. During this implementation pass, `http://127.0.0.1:8000/metrics` was unavailable.
- [ ] Live-validate cloud OpenAI and Gemini provider telemetry with real credentials.
- [ ] Add deeper parser fixture coverage for escaped labels, invalid values, summaries, counters, and provider-specific metric families.
- [ ] Add richer provider-specific normalizers for TGI, LocalAI, llama.cpp, and LM Studio metrics beyond generic OpenAI-compatible plus Prometheus handling.
- [ ] Add raw metric truncation indicator surfacing directly in dashboard table columns.
- [ ] Add direct navigation links from request details to provider telemetry sample detail. The correlated sample ID is persisted and displayed, but direct cross-page navigation is not yet implemented.

## Acceptance Criteria Status

- [x] Xeno Client collects host telemetry and provider telemetry without blocking inference.
- [x] Provider telemetry is persisted in SQLite with retention pruning.
- [x] Ollama telemetry shows available and loaded models where Ollama supports it.
- [x] vLLM-style telemetry shows Prometheus-derived runtime metrics where `/metrics` is exposed.
- [x] Generic providers degrade gracefully when only `/v1/models` is available.
- [x] Provider telemetry appears in the local dashboard.
- [x] Provider telemetry analytics supports standard dashboard timeframes and bucket behavior.
- [x] Provider telemetry details are readable and include copyable JSON.
- [x] Request details show provider telemetry context when available.
- [x] Local dashboard settings can manage provider telemetry behavior.
- [x] Docs, Postman, and SDK documentation are current.
- [x] Tests cover settings, parsing, persistence, API routes, and major local API behavior.
- [x] The implementation passed the three usability and design reviews.
- [x] The new provider telemetry code complies with the relevant `C:\Code\agents\requirements` constraints that were verified locally.


# Additional Xeno Client Provider Telemetry Plan

This plan defines the work required for Xeno Client to collect telemetry from the underlying inference and embedding software used by configured provider endpoints, store that telemetry with the existing local telemetry history, expose it in the Xeno Client dashboard, document it across the product, and prepare the protocol and data contracts for future server-requested provider telemetry.

The feature must follow the same engineering and UX standards as the existing Xeno Client telemetry work, the Xeno Server Dashboard, and RigMonitor. It must be actionable: each item is written so a developer can mark progress directly in this file.

## Implementation Status

Status: planned.

- [ ] Planning artifact created.
- [ ] Requirements compliance reviewed before implementation.
- [ ] Client provider telemetry settings implemented.
- [ ] Provider telemetry named contracts implemented.
- [ ] Provider telemetry collectors implemented.
- [ ] Local SQLite schema and migrations implemented.
- [ ] Local dashboard API implemented.
- [ ] Local dashboard pages and controls implemented.
- [ ] Request history correlation implemented.
- [ ] Server-requested telemetry contract prepared.
- [ ] Documentation updated.
- [ ] Postman updated.
- [ ] SDK documentation and generated surfaces reviewed.
- [ ] Tests implemented.
- [ ] Three usability and design passes completed.
- [ ] Final build and test verification completed.

## Reference Baseline

Use these codebases as references while implementing:

- Xeno Client telemetry:
  - `src/Xeno.Client/Telemetry`
  - `src/Xeno.Client/LocalDashboard`
  - `dashboard/src/local/LocalDashboardApp.jsx`
  - `archive/CLIENT_TELEMETRY.md`
- Xeno Server Dashboard visual and interaction patterns:
  - `dashboard/src`
- RigMonitor provider telemetry:
  - `C:\Code\rigmonitor\src\RigMonitor.Core\Models\OllamaTelemetry.cs`
  - `C:\Code\rigmonitor\src\RigMonitor.Core\Models\VllmTelemetry.cs`
  - `C:\Code\rigmonitor\src\RigMonitor.Telemetry\Services\VllmTelemetryProvider.cs`
  - `C:\Code\rigmonitor\src\RigMonitor.Core\Database\Interfaces\ITelemetryHistoryMethods.cs`
  - `C:\Code\rigmonitor\dashboard\src\views\TelemetryAnalyticsView.jsx`
- Requirements:
  - `C:\Code\agents\requirements`

RigMonitor should be used as the functional reference for Ollama, vLLM, Prometheus parsing, telemetry persistence, analytics, and historical drill-down. Xeno Server Dashboard should be used as the visual reference for shell layout, tables, chart controls, modals, dark mode, light mode, copy-to-clipboard, action menus, tooltips, and page density.

## Goals

- [ ] Collect telemetry from configured local inference and embedding provider endpoints.
- [ ] Support Ollama, vLLM, OpenAI-compatible endpoints, llama.cpp compatible servers, LM Studio compatible servers, LocalAI, Text Generation Inference, OpenAI, and Gemini where applicable.
- [ ] Store provider telemetry in the same local SQLite database used by the existing Xeno Client dashboard.
- [ ] Retain provider telemetry according to the existing telemetry retention policy.
- [ ] Expose provider telemetry in the Xeno Client dashboard with live status, charts, tables, analytics, and detail modals.
- [ ] Correlate provider telemetry with request history when requests are processed.
- [ ] Prepare future websocket protocol messages so the Xeno Server can request recent provider telemetry from connected clients.
- [ ] Avoid collecting prompts, responses, credentials, or secrets as provider telemetry.
- [ ] Keep all code compliant with `C:\Code\agents\requirements`.

## Non-Goals

- [ ] Do not implement proactive upload of provider telemetry to the Xeno Server in the first pass unless the server already has a stable receiving contract.
- [ ] Do not require provider telemetry for request routing in the first pass.
- [ ] Do not scrape cloud provider internals that are not exposed by their public APIs.
- [ ] Do not store raw prompt bodies, response bodies, API keys, bearer tokens, or provider credentials in provider telemetry tables.
- [ ] Do not introduce a new charting library.
- [ ] Do not introduce a parallel dashboard design language.

## Requirements Compliance Gate

These requirements must be enforced while writing code, not as a cleanup pass.

- [ ] C# code uses explicit types, not `var`.
- [ ] C# code does not use tuples.
- [ ] C# code does not use anonymous DTOs for any request, response, persisted payload, websocket payload, or test fixture.
- [ ] C# code does not use `JsonElement`, `JsonNode`, `JsonObject`, or `JsonArray`.
- [ ] JSON contracts use named classes with clear ownership.
- [ ] Public C# classes, enums, methods, and properties have XML documentation.
- [ ] Each C# class or enum lives in its own file.
- [ ] Async methods accept `CancellationToken` unless an owning service token already controls the lifecycle.
- [ ] Awaited calls use `.ConfigureAwait(false)` where appropriate.
- [ ] Database access uses parameterized SQL only.
- [ ] Dynamic database identifiers pass through the existing local dashboard sanitizer pattern.
- [ ] IDs use PrettyId with `{prefix}_{k-sortable}_{random}` and remain within the established 32-character limit.
- [ ] Frontend visible strings are added to the dashboard i18n catalog.
- [ ] Frontend controls include useful, descriptive tooltips.
- [ ] Copy-to-clipboard appears on IDs, JSON bodies, raw metric payloads, and request/detail JSON fields.
- [ ] Touchstone packages are used for .NET test structure.

## Data Classification And Privacy

- [ ] Classify all provider telemetry fields as operational metadata.
- [ ] Do not persist authorization headers.
- [ ] Do not persist provider credentials.
- [ ] Redact tokens found in provider URLs before persisting or displaying.
- [ ] Store endpoint host, port, path, and a stable endpoint hash for correlation.
- [ ] Store provider name and API type because these are user-configured operational values.
- [ ] Store model names because they are already advertised by Xeno Client.
- [ ] Store health, version, queue, model, throughput, token, latency, cache, memory, and runtime metrics where available.
- [ ] Store raw Prometheus samples only when explicitly enabled and bounded by size.
- [ ] Mark raw metrics as diagnostic material in UI and docs.
- [ ] Ensure the dashboard never renders raw metric labels as executable HTML.

## Settings Design

Extend `ClientTelemetrySettings` with provider telemetry settings. Provider telemetry belongs inside the existing `Telemetry` section because it is collected and retained alongside CPU, memory, disk, and GPU telemetry.

Suggested `~/.xeno/client.json` shape:

```json
{
  "Telemetry": {
    "Enabled": true,
    "CollectionIntervalMs": 15000,
    "RetentionDays": 30,
    "ProviderTelemetry": {
      "Enabled": true,
      "CollectionIntervalMs": 15000,
      "RequestTimeoutMs": 5000,
      "ConcurrentProviderLimit": 4,
      "DiscoverMetricsEndpoints": true,
      "PersistRawMetrics": false,
      "RawMetricsMaxBytes": 262144,
      "CollectModelDetails": true,
      "CollectProcessCorrelation": true,
      "CollectCloudHeaderTelemetry": true,
      "EndpointOverrides": []
    }
  }
}
```

Implementation checklist:

- [ ] Add `ClientProviderTelemetrySettings`.
- [ ] Add `ClientProviderTelemetryEndpointOverride`.
- [ ] Add `ProviderTelemetry` to `ClientTelemetrySettings`.
- [ ] Default provider telemetry to enabled.
- [ ] Default provider collection interval to `Telemetry.CollectionIntervalMs`.
- [ ] Clamp provider collection interval to `>= 5000` milliseconds.
- [ ] Default provider request timeout to `5000` milliseconds.
- [ ] Clamp provider request timeout to `>= 500` and `<= 300000` milliseconds.
- [ ] Default concurrent provider limit to `4`.
- [ ] Clamp concurrent provider limit to `>= 1` and `<= 16`.
- [ ] Default metrics endpoint discovery to enabled.
- [ ] Default raw metrics persistence to disabled.
- [ ] Clamp raw metrics max bytes to `>= 0` and `<= 1048576`.
- [ ] Add endpoint override fields for provider name, metrics URL, health URL, models URL, enabled state, and provider kind override.
- [ ] Update generated default client settings.
- [ ] Update local dashboard settings UI.
- [ ] Clearly label settings that require restart and settings that apply live.

## Provider Coverage Matrix

Provider telemetry must be best-effort by provider type. A failed provider scrape must never prevent inference requests or host telemetry collection.

| Provider Type | Collection Sources | Expected Metrics | Notes |
| --- | --- | --- | --- |
| Ollama | `/api/version`, `/api/tags`, `/api/ps`, optional process correlation | version, available model count, loaded model count, loaded model memory, expires-at, health, latency | Follow RigMonitor Ollama model patterns. |
| vLLM | `/health`, `/v1/models`, `/metrics` | running requests, waiting requests, swapped requests, prompt tokens, generation tokens, TTFT, latency buckets, GPU/KV cache usage, model names | Use Prometheus metrics. |
| OpenAI-compatible generic | `/health`, `/v1/models`, optional `/metrics` | health, models, request latency, status, Prometheus metrics when present | Avoid vendor-specific assumptions unless detected. |
| llama.cpp server | `/health`, `/v1/models`, optional `/metrics` | health, model names, request counters if exposed, token counters if exposed | Treat as OpenAI-compatible plus metrics if available. |
| LM Studio | `/v1/models`, health if available | health, model names, request timing from Xeno request history | No internal telemetry unless exposed. |
| LocalAI | `/v1/models`, optional `/metrics` | health, model names, Prometheus counters if present | Normalize Prometheus metrics conservatively. |
| Text Generation Inference | `/health`, `/metrics`, model info where exposed | queue, latency, tokens, errors, model name | Prometheus-first. |
| OpenAI cloud | request/response headers and request history only | rate limit headers, request id headers, latency from Xeno | Do not attempt internal host telemetry. |
| Gemini cloud | request/response headers and request history only | request id headers, latency from Xeno, quota headers when exposed | Do not attempt internal host telemetry. |

Implementation checklist:

- [ ] Add provider detection from configured provider endpoint `apiType`.
- [ ] Add endpoint discovery rules for `/metrics`, `/health`, `/v1/models`, `/api/version`, `/api/tags`, and `/api/ps`.
- [ ] Add conservative request timeouts and cancellation handling.
- [ ] Add per-provider failure isolation.
- [ ] Add telemetry status per provider with last success time and last failure reason.
- [ ] Add disabled status when provider telemetry is disabled globally or per endpoint.

## Named Contracts

Create named classes under `src/Xeno.Client/Telemetry/Models/Provider` or an equivalent folder. Do not reuse anonymous dictionaries as stable contracts.

Core snapshot:

- [ ] `ClientProviderTelemetrySnapshot`
- [ ] `ClientProviderTelemetryMetadata`
- [ ] `ClientProviderTelemetryProviderStatus`
- [ ] `ClientProviderTelemetryEndpoint`
- [ ] `ClientProviderTelemetryHealth`
- [ ] `ClientProviderTelemetryRuntime`
- [ ] `ClientProviderTelemetryQueue`
- [ ] `ClientProviderTelemetryTokenThroughput`
- [ ] `ClientProviderTelemetryLatency`
- [ ] `ClientProviderTelemetryCache`
- [ ] `ClientProviderTelemetryMemory`
- [ ] `ClientProviderTelemetryModel`
- [ ] `ClientProviderTelemetryLoadedModel`
- [ ] `ClientProviderTelemetryMetric`
- [ ] `ClientProviderTelemetryLabel`
- [ ] `ClientProviderTelemetryCollectionStatus`

Persistence and API models:

- [ ] `ClientProviderTelemetrySampleRecord`
- [ ] `ClientProviderTelemetryModelSampleRecord`
- [ ] `ClientProviderTelemetryMetricSampleRecord`
- [ ] `ClientProviderTelemetrySampleDetail`
- [ ] `ClientProviderTelemetryLatestResponse`
- [ ] `ClientProviderTelemetrySearchRequest`
- [ ] `ClientProviderTelemetrySearchResult`
- [ ] `ClientProviderTelemetryRollupRequest`
- [ ] `ClientProviderTelemetryRollupBucket`
- [ ] `ClientProviderTelemetryDeleteRequest`
- [ ] `ClientProviderTelemetryStatusResponse`

Enums:

- [ ] `ClientProviderTelemetryKindEnum`
- [ ] `ClientProviderTelemetryStatusEnum`
- [ ] `ClientProviderTelemetryHealthStatusEnum`
- [ ] `ClientProviderTelemetryMetricEnum`
- [ ] `ClientProviderTelemetrySourceEnum`
- [ ] `ClientProviderTelemetryEndpointDiscoveryStatusEnum`
- [ ] `ClientProviderTelemetryCollectionFailureCodeEnum`

Provider-specific typed responses:

- [ ] `OllamaVersionResponse`
- [ ] `OllamaTagsResponse`
- [ ] `OllamaModelResponse`
- [ ] `OllamaPsResponse`
- [ ] `OpenAiModelsResponse`
- [ ] `OpenAiModelResponse`
- [ ] `PrometheusMetricDocument`
- [ ] `PrometheusMetricSeries`
- [ ] `PrometheusMetricLabel`
- [ ] `PrometheusMetricSample`
- [ ] `ProviderHealthProbeResponse`

Contract requirements:

- [ ] All contracts use stable camelCase serialization.
- [ ] Provider-specific opaque strings are preserved as strings but isolated to provider-specific fields.
- [ ] Classification values owned by Xeno use enums.
- [ ] Raw metric labels use named label objects, not dictionaries, unless requirements explicitly allow dictionaries for opaque key/value metadata.
- [ ] Any key/value metadata uses a named `Name` and `Value` class if dictionary use conflicts with requirements.

## Provider Collector Architecture

Add a provider telemetry subsystem parallel to, but separate from, host telemetry collection.

Suggested folder structure:

```text
src/Xeno.Client/Telemetry/
  Abstractions/
    IClientProviderTelemetryCollector.cs
    IClientProviderTelemetryCollectorFactory.cs
    IClientProviderTelemetryHistoryMethods.cs
    IClientProviderTelemetryService.cs
    IPrometheusMetricsParser.cs
  Models/
    Provider/
  Providers/
    ProviderTelemetry/
      OllamaProviderTelemetryCollector.cs
      VllmProviderTelemetryCollector.cs
      OpenAiCompatibleProviderTelemetryCollector.cs
      CloudProviderTelemetryCollector.cs
      ProviderTelemetryCollectorFactory.cs
      PrometheusMetricsParser.cs
  Services/
    ClientProviderTelemetryService.cs
    ClientProviderTelemetryPersistenceService.cs
```

Implementation checklist:

- [ ] Add `IClientProviderTelemetryCollector`.
- [ ] Add `IClientProviderTelemetryCollectorFactory`.
- [ ] Add `IClientProviderTelemetryService`.
- [ ] Add `IPrometheusMetricsParser`.
- [ ] Add shared HTTP helper for bounded provider telemetry requests.
- [ ] Add one collector per provider family.
- [ ] Add a fallback generic OpenAI-compatible collector.
- [ ] Add a cloud collector for OpenAI and Gemini that records reachability and header-derived telemetry only.
- [ ] Add a provider telemetry service that sweeps configured provider endpoints.
- [ ] Add a provider telemetry persistence service that runs on the configured interval.
- [ ] Ensure provider telemetry collection does not block `xeno connect`, request handling, or local dashboard startup.
- [ ] Add `LatestSnapshot` and runtime status similar to existing `ClientTelemetryPersistenceService`.

## Prometheus Metrics Parsing

vLLM, TGI, LocalAI, llama.cpp, and other compatible systems may expose Prometheus metrics. Implement one reusable parser and provider-specific normalizers.

Parser requirements:

- [ ] Accept raw text and return named `PrometheusMetricDocument`.
- [ ] Ignore comments and invalid lines.
- [ ] Parse metric name, labels, numeric value, and optional timestamp.
- [ ] Represent labels with named `PrometheusMetricLabel` objects.
- [ ] Avoid `JsonElement`, `JsonNode`, dictionaries, tuples, and anonymous types.
- [ ] Preserve unknown metrics as bounded diagnostic data when raw metric persistence is enabled.
- [ ] Enforce maximum raw metrics size before parsing.
- [ ] Unit test escaped labels, comma-separated labels, missing labels, invalid values, histogram buckets, counters, gauges, and summaries.

Normalization requirements:

- [ ] Normalize vLLM queue metrics.
- [ ] Normalize vLLM request latency metrics.
- [ ] Normalize vLLM time-to-first-token metrics.
- [ ] Normalize vLLM prompt and generation token counters.
- [ ] Normalize vLLM KV cache and GPU cache metrics.
- [ ] Normalize TGI request, token, queue, and latency metrics where available.
- [ ] Normalize LocalAI and llama.cpp compatible metrics where recognizable.
- [ ] Store unknown recognized-provider metrics in raw/diagnostic rows only when enabled.

## Persistence Design

Provider telemetry must use the existing local SQLite database architecture and sanitizer pattern.

Add a separate method group:

- [ ] `IClientProviderTelemetryHistoryMethods`
- [ ] `SqliteClientProviderTelemetryHistoryMethods`
- [ ] `LocalDashboardDatabaseDriverBase.ProviderTelemetryHistory`
- [ ] `SqliteLocalDashboardDatabaseDriver.ProviderTelemetryHistory`

Suggested tables:

```sql
local_provider_telemetry_samples
local_provider_telemetry_models
local_provider_telemetry_metrics
local_provider_telemetry_labels
```

`local_provider_telemetry_samples` fields:

- [ ] `id`
- [ ] `client_id`
- [ ] `provider_name`
- [ ] `provider_api_type`
- [ ] `provider_kind`
- [ ] `endpoint_hash`
- [ ] `endpoint_display`
- [ ] `collected_utc`
- [ ] `status`
- [ ] `health_status`
- [ ] `version`
- [ ] `available_model_count`
- [ ] `loaded_model_count`
- [ ] `active_request_count`
- [ ] `queued_request_count`
- [ ] `waiting_request_count`
- [ ] `swapped_request_count`
- [ ] `prompt_tokens_per_second`
- [ ] `generation_tokens_per_second`
- [ ] `total_tokens_per_second`
- [ ] `request_rate_per_second`
- [ ] `error_rate_per_second`
- [ ] `latency_p50_ms`
- [ ] `latency_p95_ms`
- [ ] `latency_p99_ms`
- [ ] `time_to_first_token_p50_ms`
- [ ] `time_to_first_token_p95_ms`
- [ ] `cache_usage_percent`
- [ ] `gpu_cache_usage_percent`
- [ ] `cpu_cache_usage_percent`
- [ ] `memory_used_bytes`
- [ ] `memory_total_bytes`
- [ ] `raw_metrics_truncated`
- [ ] `raw_metrics_bytes`
- [ ] `snapshot_json`
- [ ] `last_error`

`local_provider_telemetry_models` fields:

- [ ] `id`
- [ ] `sample_id`
- [ ] `provider_name`
- [ ] `model_name`
- [ ] `model_family`
- [ ] `quantization`
- [ ] `parameter_size`
- [ ] `context_length`
- [ ] `loaded`
- [ ] `size_bytes`
- [ ] `memory_bytes`
- [ ] `expires_utc`
- [ ] `active_request_count`
- [ ] `queued_request_count`
- [ ] `prompt_tokens_per_second`
- [ ] `generation_tokens_per_second`
- [ ] `latency_p95_ms`
- [ ] `time_to_first_token_p95_ms`

`local_provider_telemetry_metrics` fields:

- [ ] `id`
- [ ] `sample_id`
- [ ] `provider_name`
- [ ] `model_name`
- [ ] `metric_name`
- [ ] `metric_kind`
- [ ] `metric_value`
- [ ] `metric_unit`
- [ ] `source`

Indexes:

- [ ] `provider_name, collected_utc`
- [ ] `provider_kind, collected_utc`
- [ ] `model_name, collected_utc`
- [ ] `metric_name, collected_utc`
- [ ] `status, collected_utc`
- [ ] `endpoint_hash, collected_utc`
- [ ] `sample_id` on child tables

Persistence requirements:

- [ ] Schema migration runs idempotently on local dashboard database startup.
- [ ] Sanitizer allow-list includes new provider telemetry tables.
- [ ] All SQL uses parameters.
- [ ] Delete sample cascades or explicitly deletes child rows in a transaction.
- [ ] Retention pruning removes provider telemetry with host telemetry.
- [ ] Search supports paging, provider, model, status, metric, and time range filters.
- [ ] Rollups emit empty buckets for all selected ranges.
- [ ] Rollup bucket counts match dashboard chart standards: Last Hour 60, Last Day 96, Last Week 84, Last Month 90.

## Request History Correlation

Provider telemetry should make request details more useful without collecting sensitive request content beyond what Xeno already captures.

Implementation checklist:

- [ ] Add optional provider telemetry sample ID to local request history.
- [ ] Store nearest provider telemetry sample before request start.
- [ ] Store nearest provider telemetry sample after request completion when available.
- [ ] Store provider health status at request start.
- [ ] Store provider queue depth at request start when available.
- [ ] Store provider model loaded state at request start when available.
- [ ] Add provider telemetry summary to local request details modal.
- [ ] Add links from request details to provider telemetry sample details.
- [ ] Add equivalent future fields to server request history DTOs only if server-requested telemetry is implemented.

## Local Dashboard API

Add localhost-only API routes served by the Xeno Client background process.

Routes:

- [ ] `GET /api/v1/telemetry/providers/status`
- [ ] `GET /api/v1/telemetry/providers/latest`
- [ ] `POST /api/v1/telemetry/providers/collect`
- [ ] `POST /api/v1/telemetry/providers/history/search`
- [ ] `POST /api/v1/telemetry/providers/history/rollups`
- [ ] `GET /api/v1/telemetry/providers/history/{id}`
- [ ] `DELETE /api/v1/telemetry/providers/history/{id}`
- [ ] `POST /api/v1/telemetry/providers/history/delete`

API requirements:

- [ ] Reuse local dashboard token behavior.
- [ ] Use named request and response classes.
- [ ] Use the same error response model as other local dashboard APIs.
- [ ] Support `range` query defaults for Last Hour, Last Day, Last Week, and Last Month.
- [ ] Support provider and model filters.
- [ ] Support metric selection.
- [ ] Return copied JSON-safe objects for details and raw metrics.
- [ ] Document all routes in `REST_API.md` and client getting started docs.

## Local Dashboard UX

Add provider telemetry to the local dashboard without making the lower navigation noisy.

Recommended navigation:

- Administration
  - Logs
  - Telemetry
  - Provider Telemetry
  - Telemetry Analytics
  - Provider Telemetry Analytics
  - Settings

If this feels too dense during design pass 1, use tabs inside `Telemetry` and `Telemetry Analytics`:

- Telemetry page tabs: `Host`, `Providers`
- Telemetry Analytics page tabs: `Host`, `Providers`

The implementation must choose one pattern and apply it consistently.

Provider Telemetry page:

- [ ] Show current provider health table.
- [ ] Show provider KPI indicators for healthy providers, unavailable providers, loaded models, active requests, queued requests, token throughput, and error rate.
- [ ] Show full-width provider telemetry chart.
- [ ] Support timeframe selection with Last Hour, Last Day, Last Week, Last Month.
- [ ] Support chart auto-refresh dropdown defaulting to 15 seconds.
- [ ] Support metric dropdown oriented to the right.
- [ ] Support provider filter.
- [ ] Support model filter.
- [ ] Show a table of latest provider samples.
- [ ] Row click opens detail modal.
- [ ] Action menu supports View JSON and Delete.
- [ ] ID columns use fixed-width font and copy-to-clipboard.

Provider Telemetry Analytics page:

- [ ] Show time selector outside the chart card.
- [ ] Time selector applies to all page controls.
- [ ] Metric dropdown is right-oriented and visually separated from timeframe controls.
- [ ] Provider and model filters are compact and aligned with other table controls.
- [ ] Show full-width chart.
- [ ] Show rollup table.
- [ ] Show sample history table.
- [ ] Table controls match Xeno Server Dashboard and local request history controls.
- [ ] Auto-refresh defaults to 15 seconds.
- [ ] Action menu cannot render off screen near the bottom of the viewport.

Provider detail modal:

- [ ] Modal consumes 95 percent of viewport width and height.
- [ ] Summary uses a table layout, not stacks of cards.
- [ ] Row heights remain consistent when copy-to-clipboard controls are present.
- [ ] Health, endpoint, version, model counts, queue, throughput, cache, and latency are grouped clearly.
- [ ] Request-correlated sample links are shown when available.
- [ ] Models table is full width.
- [ ] Metrics table is full width.
- [ ] Raw metrics and snapshot JSON are collapsible and expanded by default.
- [ ] JSON fields include copy-to-clipboard controls.
- [ ] Timestamps are visible and consistently formatted.
- [ ] No all-caps field names inside detail content unless used as a table header style already established by the dashboard.

Settings UI:

- [ ] Add provider telemetry enabled toggle.
- [ ] Add collection interval field.
- [ ] Add request timeout field.
- [ ] Add concurrent provider limit field.
- [ ] Add metrics endpoint discovery toggle.
- [ ] Add raw metrics persistence toggle.
- [ ] Add raw metrics byte limit field.
- [ ] Add collect model details toggle.
- [ ] Add collect process correlation toggle.
- [ ] Add collect cloud header telemetry toggle.
- [ ] Label restart-required settings clearly.
- [ ] Use compact field heights consistent with existing settings controls.

## Chart And Table Standards

Apply these standards to all new provider telemetry charts and fix shared helpers if necessary.

- [ ] Last Hour uses 60 buckets.
- [ ] Last Day uses 96 buckets.
- [ ] Last Week uses 84 buckets.
- [ ] Last Month uses 90 buckets.
- [ ] Charts show at most 5 Y-axis labels.
- [ ] Charts show at most 8 X-axis labels.
- [ ] Empty charts show 0 at bottom and 1 at top.
- [ ] Chart hover tooltips render near the pointer and above workspace content.
- [ ] Charts consume the full available width of their card.
- [ ] Charts consume the intended vertical area of their card.
- [ ] Tables use the shared pagination, filtering, refresh, and auto-refresh controls.
- [ ] Pressing Enter in a column filter applies the filter.
- [ ] Table hover colors are readable in dark mode.
- [ ] Table text colors are readable in dark mode.
- [ ] Action menus stay within viewport bounds.

## Server-Requested Telemetry Preparation

Do not force server upload into this phase, but create contracts that make it straightforward.

Future websocket frames:

- [ ] `server.providerTelemetry.request`
- [ ] `client.providerTelemetry.snapshot`
- [ ] `client.providerTelemetry.history`
- [ ] `client.providerTelemetry.error`
- [ ] `server.providerTelemetry.ack`

Future protocol fields:

- [ ] Request ID.
- [ ] Tenant ID.
- [ ] Client ID.
- [ ] Requested provider names.
- [ ] Requested model names.
- [ ] Time range.
- [ ] Maximum sample count.
- [ ] Include raw metrics flag.
- [ ] Snapshot payload.
- [ ] History payload.
- [ ] Error payload.

Implementation preparation:

- [ ] Add named websocket DTOs if they can be added without enabling runtime behavior.
- [ ] Document these frames as planned or reserved in `WEBSOCKETS_API.md`.
- [ ] Ensure provider telemetry snapshot contracts can be serialized over websocket without local-only dependencies.
- [ ] Ensure raw metrics remain optional and bounded.
- [ ] Ensure server requests cannot force the client to expose credentials or prompt/response content.

## Backend And Server Dashboard Considerations

This feature primarily belongs to Xeno Client. Server changes should be limited unless server-requested telemetry is implemented in the same pass.

- [ ] Review whether server SDKs need new types for future provider telemetry frames.
- [ ] If server REST routes are not added, document that provider telemetry is local-dashboard-only for now.
- [ ] If websocket DTOs are added, update `WEBSOCKETS_API.md`.
- [ ] If server storage is added later, reuse existing server database interface/implementation style and all supported database providers.
- [ ] Do not add server dashboard pages until the server actually receives provider telemetry.

## Documentation Updates

Update documentation in the same implementation pass.

- [ ] `README.md`: describe provider telemetry as part of the client-local observability story.
- [ ] `CLIENT_GETTING_STARTED.md`: explain enabling, configuring, viewing, and troubleshooting provider telemetry.
- [ ] `REST_API.md`: document local provider telemetry dashboard API routes.
- [ ] `WEBSOCKETS_API.md`: document reserved or implemented server-requested provider telemetry frames.
- [ ] `CHANGELOG.md`: add the feature entry.
- [ ] `Postman` collection: add local dashboard provider telemetry routes.
- [ ] Local dashboard docs: include the Provider Telemetry and Provider Telemetry Analytics pages.
- [ ] SDK READMEs: clarify whether provider telemetry is local-dashboard-only or available through SDKs.
- [ ] `TESTING.md` files: include provider telemetry tests where relevant.

Documentation must state:

- [ ] Provider telemetry is best-effort.
- [ ] Cloud providers expose limited telemetry.
- [ ] Ollama and vLLM expose richer telemetry when their local endpoints are reachable.
- [ ] Raw metrics persistence is optional and bounded.
- [ ] Provider telemetry does not collect prompts, responses, or credentials.
- [ ] Provider telemetry retention follows local telemetry retention.

## Postman Updates

Update the Postman collection with a `Local Dashboard - Provider Telemetry` folder.

- [ ] Status.
- [ ] Latest.
- [ ] Collect Now.
- [ ] Search History.
- [ ] Rollups.
- [ ] Read Detail.
- [ ] Delete One.
- [ ] Delete All.
- [ ] Include local dashboard token variable.
- [ ] Include sample request bodies.
- [ ] Include examples for provider and model filters.

## SDK Updates

Provider telemetry APIs are local dashboard APIs. Review all SDKs to avoid stale documentation or missing generated surfaces.

- [ ] C# SDK reviewed.
- [ ] JavaScript SDK reviewed.
- [ ] Python SDK reviewed.
- [ ] If SDKs intentionally target server APIs only, document that provider telemetry remains local dashboard API only.
- [ ] If local dashboard SDK support exists, add provider telemetry methods and tests.
- [ ] Update SDK README files.
- [ ] Update SDK interactive console applications if provider telemetry methods are added.
- [ ] Update SDK test harnesses if provider telemetry methods are added.

## Test Plan

Use Touchstone-based .NET test patterns already present in Xeno.

Unit tests:

- [ ] Settings defaults.
- [ ] Settings clamp behavior.
- [ ] Provider endpoint override behavior.
- [ ] Provider detection from API type.
- [ ] Metrics endpoint discovery.
- [ ] Prometheus parser with labels.
- [ ] Prometheus parser with escaped labels.
- [ ] Prometheus parser with invalid lines.
- [ ] vLLM metric normalization.
- [ ] Ollama version response parsing.
- [ ] Ollama tags response parsing.
- [ ] Ollama ps response parsing.
- [ ] OpenAI-compatible models response parsing.
- [ ] Cloud provider telemetry redaction.
- [ ] Endpoint hash stability.
- [ ] PrettyId format and length for provider telemetry sample IDs.

Persistence tests:

- [ ] SQLite schema initializes on empty database.
- [ ] Schema migration is idempotent.
- [ ] Create sample persists parent row.
- [ ] Create sample persists model rows.
- [ ] Create sample persists metric rows.
- [ ] Read latest returns latest by collected UTC.
- [ ] Read detail returns models and metrics.
- [ ] Search filters by provider.
- [ ] Search filters by model.
- [ ] Search filters by metric.
- [ ] Search filters by status.
- [ ] Rollups return empty buckets.
- [ ] Rollups return expected bucket counts.
- [ ] Delete one removes child rows.
- [ ] Delete all removes child rows.
- [ ] Retention pruning removes old provider telemetry.
- [ ] SQL parameters are used for user-supplied filters.

Service tests:

- [ ] Provider telemetry service sweeps multiple endpoints.
- [ ] One provider failure does not fail the full sweep.
- [ ] Timeout returns unavailable status.
- [ ] Disabled provider is skipped.
- [ ] Concurrent provider limit is enforced.
- [ ] Collection can be cancelled.
- [ ] Latest snapshot updates after successful collection.
- [ ] Runtime status records last success and last failure.

Local dashboard API tests:

- [ ] Status route.
- [ ] Latest route.
- [ ] Collect route.
- [ ] Search route.
- [ ] Rollup route.
- [ ] Detail route.
- [ ] Delete route.
- [ ] Delete-all route.
- [ ] Unauthorized local token behavior.
- [ ] Error response shape.

Frontend tests and verification:

- [ ] Dashboard build succeeds.
- [ ] Provider Telemetry page renders with no data.
- [ ] Provider Telemetry page renders with fixture data.
- [ ] Provider Telemetry Analytics page renders with no data.
- [ ] Provider Telemetry Analytics page renders with fixture data.
- [ ] Chart tooltip placement is correct.
- [ ] Action menu stays on screen.
- [ ] Dark mode table text is readable.
- [ ] Light mode table text is readable.
- [ ] Copy-to-clipboard state changes to success indicator.
- [ ] Row click opens detail modal.
- [ ] Detail modal uses 95 percent viewport sizing.
- [ ] JSON copy buttons appear on JSON fields.
- [ ] Enter in filter fields applies table filters.

Manual integration tests:

- [ ] Ollama running locally.
- [ ] vLLM with `/metrics` enabled.
- [ ] Generic OpenAI-compatible endpoint with `/v1/models`.
- [ ] Cloud OpenAI configuration.
- [ ] Cloud Gemini configuration.
- [ ] Provider unavailable.
- [ ] Provider endpoint returns invalid metrics.
- [ ] Raw metrics disabled.
- [ ] Raw metrics enabled and bounded.

## Three Usability And Design Passes

The feature is not complete until all three passes are performed and any findings are fixed or explicitly deferred.

### Pass 1: Information Architecture And Workflow

Compare the implemented flow against Xeno Server Dashboard, Xeno Client Dashboard, and RigMonitor.

- [ ] Confirm provider telemetry is placed where operators expect it.
- [ ] Confirm navigation does not feel cluttered.
- [ ] Confirm telemetry and telemetry analytics pages are easy to distinguish.
- [ ] Confirm provider/model filters are available where needed.
- [ ] Confirm the workflow from chart to table to detail modal is obvious.
- [ ] Confirm settings are discoverable.
- [ ] Confirm disabled/unavailable providers are understandable.
- [ ] Confirm cloud provider limitations are explained without cluttering the UI.
- [ ] Fix findings from pass 1.

### Pass 2: Visual Design, Layout, And Responsiveness

Evaluate every page, modal, card, table, chart, control, label, and tooltip.

- [ ] Check desktop layout.
- [ ] Check narrow viewport layout.
- [ ] Check light mode.
- [ ] Check dark mode.
- [ ] Check table row height consistency.
- [ ] Check fixed-width font use for IDs and JSON.
- [ ] Check chart sizing.
- [ ] Check chart hover tooltip placement.
- [ ] Check action menu placement near viewport edges.
- [ ] Check button icon sizing and tooltips.
- [ ] Check field heights.
- [ ] Check casing of all titles and labels.
- [ ] Fix findings from pass 2.

### Pass 3: Usability, Accessibility, And Operational States

Evaluate every interaction and every state.

- [ ] No-data state.
- [ ] Loading state.
- [ ] Error state.
- [ ] Provider unavailable state.
- [ ] Provider disabled state.
- [ ] Permission/token failure state.
- [ ] Slow provider collection state.
- [ ] Raw metrics truncated state.
- [ ] Copy-to-clipboard success and failure.
- [ ] Keyboard navigation through filters, dropdowns, buttons, tabs, and modals.
- [ ] Enter key behavior in filters and forms.
- [ ] Screen-reader labels for icon-only buttons.
- [ ] Tooltip wording is descriptive, not a restatement of the label.
- [ ] Fix findings from pass 3.

## Implementation Phases

### Phase 1: Baseline Audit

- [ ] Re-read `C:\Code\agents\requirements`.
- [ ] Re-read current Xeno Client telemetry files.
- [ ] Re-read RigMonitor Ollama and vLLM telemetry files.
- [ ] Re-read Xeno local dashboard pages and shared controls.
- [ ] Confirm current dashboard chart/table helper behavior.
- [ ] Confirm current local SQLite migration pattern.
- [ ] Record any implementation constraints in this plan.

### Phase 2: Settings And Contracts

- [ ] Implement provider telemetry settings classes.
- [ ] Add settings defaults and clamps.
- [ ] Add provider telemetry enums.
- [ ] Add provider telemetry snapshot classes.
- [ ] Add provider telemetry persistence classes.
- [ ] Add provider-specific typed response classes.
- [ ] Add websocket reserved DTOs if implementing protocol preparation in code.
- [ ] Add unit tests for defaults, clamps, and serialization.

### Phase 3: Collectors

- [ ] Implement Prometheus parser.
- [ ] Implement Ollama collector.
- [ ] Implement vLLM collector.
- [ ] Implement generic OpenAI-compatible collector.
- [ ] Implement cloud provider collector.
- [ ] Implement collector factory.
- [ ] Implement provider endpoint discovery.
- [ ] Implement process correlation if enabled.
- [ ] Add collector tests with fixtures.

### Phase 4: Persistence

- [ ] Add provider telemetry history interface.
- [ ] Add SQLite implementation.
- [ ] Add schema migration.
- [ ] Add sanitizer entries.
- [ ] Add search.
- [ ] Add rollups.
- [ ] Add detail read.
- [ ] Add delete.
- [ ] Add retention pruning.
- [ ] Add persistence tests.

### Phase 5: Runtime Integration

- [ ] Add provider telemetry service.
- [ ] Add provider telemetry persistence service.
- [ ] Start service with background `xeno` process when telemetry is enabled.
- [ ] Add collect-now support.
- [ ] Add runtime status tracking.
- [ ] Add request history correlation.
- [ ] Ensure service shutdown is clean.
- [ ] Add service tests.

### Phase 6: Local Dashboard API

- [ ] Add status route.
- [ ] Add latest route.
- [ ] Add collect route.
- [ ] Add search route.
- [ ] Add rollup route.
- [ ] Add detail route.
- [ ] Add delete route.
- [ ] Add delete-all route.
- [ ] Add route tests.

### Phase 7: Local Dashboard UI

- [ ] Add navigation.
- [ ] Add i18n strings.
- [ ] Add provider telemetry API client helpers.
- [ ] Add provider telemetry chart.
- [ ] Add provider telemetry latest table.
- [ ] Add provider telemetry analytics table.
- [ ] Add provider telemetry detail modal.
- [ ] Add provider telemetry settings controls.
- [ ] Add copy-to-clipboard controls.
- [ ] Add tooltips for every new control.
- [ ] Add no-data/loading/error states.
- [ ] Build dashboard.

### Phase 8: Server Protocol Preparation

- [ ] Update `WEBSOCKETS_API.md`.
- [ ] Add reserved websocket DTOs if appropriate.
- [ ] Add tests for DTO serialization if DTOs are added.
- [ ] Clearly document that server-requested telemetry is prepared but not enabled, unless fully implemented.

### Phase 9: Documentation, Postman, SDKs

- [ ] Update `README.md`.
- [ ] Update `CLIENT_GETTING_STARTED.md`.
- [ ] Update `REST_API.md`.
- [ ] Update `WEBSOCKETS_API.md`.
- [ ] Update `CHANGELOG.md`.
- [ ] Update Postman collection.
- [ ] Review SDK README files.
- [ ] Update SDKs only if server or SDK-supported local API surfaces changed.
- [ ] Update testing docs.

### Phase 10: Usability And Design Passes

- [ ] Complete pass 1.
- [ ] Fix pass 1 findings.
- [ ] Complete pass 2.
- [ ] Fix pass 2 findings.
- [ ] Complete pass 3.
- [ ] Fix pass 3 findings.

### Phase 11: Final Verification

- [ ] Run C# format or style checks used by the repo.
- [ ] Run `dotnet build`.
- [ ] Run Touchstone-backed .NET tests.
- [ ] Run dashboard build.
- [ ] Run dashboard tests if present.
- [ ] Run SDK tests if SDKs changed.
- [ ] Run local client with telemetry enabled.
- [ ] Verify local dashboard starts.
- [ ] Verify provider telemetry page with at least one reachable provider.
- [ ] Verify provider telemetry page with an unavailable provider.
- [ ] Verify no prompt, response, or credential content is persisted.
- [ ] Update this plan with final status.

## Acceptance Criteria

The feature is complete when:

- [ ] Xeno Client collects host telemetry and provider telemetry without blocking inference.
- [ ] Provider telemetry is persisted in SQLite with retention pruning.
- [ ] Ollama telemetry shows available and loaded models where Ollama supports it.
- [ ] vLLM telemetry shows Prometheus-derived runtime metrics where vLLM exposes them.
- [ ] Generic and cloud providers degrade gracefully with clear availability and limited telemetry.
- [ ] Provider telemetry appears in the local dashboard.
- [ ] Provider telemetry analytics supports all standard timeframes and bucket counts.
- [ ] Provider telemetry details are readable, full-height/full-width, and include copyable JSON.
- [ ] Request details can show provider telemetry context when available.
- [ ] Local dashboard settings can manage provider telemetry behavior.
- [ ] Docs, Postman, and SDK documentation are current.
- [ ] Tests cover settings, parsing, persistence, API routes, and major UI states.
- [ ] The implementation passes the three usability and design reviews.
- [ ] The code complies with `C:\Code\agents\requirements`.

## Open Decisions

- [ ] Decide whether provider telemetry receives separate navigation pages or tabs inside existing Telemetry pages.
- [ ] Decide whether raw Prometheus metrics are persisted as rows, snapshot JSON only, or both when enabled.
- [ ] Decide whether process correlation should be enabled by default for local providers.
- [ ] Decide whether server-requested telemetry DTOs are implemented now or documented as reserved only.
- [ ] Decide whether local dashboard SDK support should be added or explicitly documented as out of scope.


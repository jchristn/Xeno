# Server-Requested Client Telemetry Collection Plan

Status: implementation in progress.

Last updated: 2026-06-27.

## Purpose

Xeno Client now collects host telemetry and provider telemetry for its local dashboard. Xeno Server should become the fleet-level collector for the same data: it should periodically ask each connected client for telemetry over the existing WebSocket connection, persist the received samples in the server database, expose them through authenticated server APIs, show them in the server dashboard under `CONTROL > Client Telemetry`, and make fresh telemetry usable by routing policies.

The server controls collection cadence. Clients answer requests from the server; they do not decide how often the server stores fleet telemetry. A disabled server setting must stop server collection even when clients continue collecting local telemetry for their own local dashboards.

## Assumptions To Confirm During Implementation

- [x] Treat "client telemetry" as both host telemetry and provider telemetry. Host telemetry covers CPU, memory, disk, and GPU. Provider telemetry covers provider health, loaded models, queue depth, latency, token rates, cache pressure, and normalized Prometheus metrics where available.
- [x] Preserve the client-local telemetry database as a separate feature. Server collection should not require the client local dashboard to be enabled.
- [x] Keep the existing WebSocket endpoint, authentication headers, and tenant boundary. New telemetry frames travel on the authenticated client WebSocket session.
- [x] Store server-collected telemetry in relational server tables, not only inside `xeno_platform_state`.
- [ ] Keep legacy `/api/v1/...` aliases where this repository already provides them, but document `/v1.0/api/...` as the canonical surface.
- [x] SDKs should wrap stable server REST APIs for collected telemetry. They should not wrap local-dashboard APIs.

## Non-Negotiable Requirements

- [x] Server settings include `TelemetryCollection.Enabled`.
- [x] Server settings include `TelemetryCollection.CollectionIntervalSeconds`.
- [x] `TelemetryCollection.CollectionIntervalSeconds` is clamped to `>= 10` and `<= 300`.
- [x] Server settings include bounded request timeout, retention, raw JSON persistence, and provider telemetry controls.
- [x] The server periodically requests telemetry from every connected, registered, tenant-scoped client when telemetry collection is enabled.
- [x] Collection never blocks inference routing or client heartbeats.
- [ ] The server persists collected telemetry in the configured database with tables and indexes for host samples, GPU samples, provider samples, provider model samples, provider metric samples, collection attempts, and latest per-client lookup. SQLite persistence and in-memory latest lookup are implemented; provider parity remains pending.
- [ ] SQLite, MySQL, PostgreSQL, and SQL Server receive equivalent schemas and data access methods.
- [x] The server dashboard adds `CONTROL > Client Telemetry` with charts that match the Xeno Client local dashboard look and feel.
- [ ] Routing policies can include telemetry-based filters and ranking rules, using Conductor-style metric catalog, validation, freshness handling, and diagnostics.
- [ ] Docs, SDKs, tests, OpenAPI metadata, and the Postman collection are updated in the same implementation stream. Docs, SDK wrappers, basic verification, and Postman route coverage are in progress; OpenAPI route detail and telemetry-specific tests remain pending.
- [ ] The final work includes a documentation and Postman correctness pass that verifies every documented capability and every documented API.

## Requirements Compliance Gate

Apply this gate to every implementation phase, not only at the end.

- [x] C# uses explicit types, not `var`.
- [x] C# does not use tuple returns.
- [x] `using` statements stay inside namespace blocks.
- [x] Public C# classes, enums, constructors, properties, and methods include XML documentation.
- [x] One C# class or enum per file.
- [x] Async methods that do cancellable work accept `CancellationToken`, unless the owning class already holds the cancellation token.
- [x] Awaited calls in server/library code use `.ConfigureAwait(false)`.
- [x] Fixed JSON contracts use named DTO classes, not `JsonElement`, `JsonNode`, `JsonObject`, anonymous objects in protocol models, or dictionary spelunking for known shapes.
- [x] Numeric settings use backing fields and clamps where ranges matter.
- [x] SQL is parameterized. Dynamic identifiers, if any, use an approved sanitizer.
- [x] Tenant isolation appears in every database query and every API route.
- [x] Dashboard text, labels, tooltips, empty states, chart legends, and accessibility strings flow through i18n resources.
- [ ] Dates, times, durations, byte counts, percentages, and numbers use locale-aware formatting helpers.
- [x] Dashboard charts use existing SVG/component patterns. Do not add a charting library.
- [x] .NET tests use the Touchstone shared descriptor pattern in `src/Test.Shared`, with xUnit and NUnit runners.

## Current System Baseline

Xeno Server currently stores most platform state as a serialized `PlatformState` blob in `xeno_platform_state`. That approach is acceptable for current lightweight entities, but it is the wrong storage shape for time-series telemetry. Telemetry needs range scans, rollups, retention cleanup, latest-sample lookup, client filters, metric filters, and routing-time freshness checks. Those are table-and-index problems.

Relevant current files:

- [x] `src/Xeno.Core/Settings/XenoSettings.cs`: add the root server telemetry collection settings.
- [x] `src/Xeno.Core/Settings/WebSocketSettings.cs`: keep inference request timeout separate from telemetry request timeout.
- [x] `src/Xeno.Server/XenoServer.cs`: currently owns WebSocket dispatch, REST route registration, settings persistence, OpenAPI metadata, and routing request relay.
- [x] `src/Xeno.Core/Database/DatabaseDriverBase.cs`: currently exposes `Platform` and serialized state methods. Add telemetry method interfaces here.
- [x] `src/Xeno.Core/Database/Sqlite/SqliteDatabaseDriver.cs`
- [ ] `src/Xeno.Core/Database/Mysql/MysqlDatabaseDriver.cs`
- [ ] `src/Xeno.Core/Database/Postgresql/PostgresqlDatabaseDriver.cs`
- [ ] `src/Xeno.Core/Database/SqlServer/SqlServerDatabaseDriver.cs`
- [ ] `src/Xeno.Core/Services/RoutingService.cs`: currently filters by model, client state, heartbeat, capacity, and policy match.
- [x] `src/Xeno.Core/Models/RoutingPolicy.cs`
- [x] `src/Xeno.Core/Models/RoutingPolicySaveRequest.cs`
- [x] `src/Xeno.Client/Program.cs`: currently ignores all non-`server.request` frames in the main WebSocket loop.
- [ ] `src/Xeno.Client/Telemetry/*`: source of host and provider telemetry contracts/services.
- [x] `dashboard/src/App.jsx`: server dashboard navigation, settings, routing policies, clients, request history, and API explorer.
- [ ] `dashboard/src/local/LocalDashboardApp.jsx`: visual and workflow reference for host/provider telemetry pages.
- [ ] `dashboard/src/components/ActivityChart.jsx`: chart controls and SVG style reference.
- [x] `WEBSOCKETS_API.md`, `REST_API.md`, `MODEL_ROUTING.md`, `README.md`, `CLIENT_GETTING_STARTED.md`, `CHANGELOG.md`, `Xeno.postman_collection.json`.

## Target Product Behavior

When enabled, Xeno Server starts a background collection loop after database initialization and WebSocket route registration. On every configured interval, the loop snapshots the current connected clients and sends each one a telemetry request frame. Each client responds with one or more typed frames containing latest host telemetry and provider telemetry. The server validates tenant and client IDs, persists successful samples, records failures, updates an in-memory latest-telemetry cache for routing, and acknowledges the response.

Operators can then:

- [x] See fleet telemetry in `CONTROL > Client Telemetry`.
- [ ] Filter by tenant when authorized, client, client name, provider, model, metric, time range, and telemetry freshness.
- [x] Compare host metrics across connected clients.
- [x] Inspect per-client provider health and queue pressure.
- [ ] Open a sample detail modal with raw JSON, normalized host metrics, GPU devices, provider models, and provider metrics.
- [x] Delete old telemetry where authorized.
- [ ] Tune collection settings in server settings.
- [ ] Author routing policy conditions that require, exclude, or rank clients based on telemetry.
- [ ] Use API Explorer, SDKs, and Postman to exercise every supported endpoint. SDK and Postman route coverage exists; live API Explorer/Postman correctness verification remains pending.

## Phase 1: Settings And Configuration

Add a dedicated settings class instead of folding cadence into `WebSocketSettings`. The collection loop is a server telemetry concern, not a socket transport concern.

Implementation status: completed for Core settings and sample config files; dashboard settings normalization and live reload remain pending.

Implementation checklist:

- [x] Add `src/Xeno.Core/Settings/TelemetryCollectionSettings.cs`.
- [x] Add `public TelemetryCollectionSettings TelemetryCollection { get; set; } = new TelemetryCollectionSettings();` to `XenoSettings`.
- [x] Add XML docs that state defaults and ranges.
- [x] Add backing fields and clamps:
  - [x] `Enabled`: default `false` for upgrade safety unless product explicitly chooses default-on.
  - [x] `CollectionIntervalSeconds`: default `60`, clamp `10` to `300`.
  - [x] `RequestTimeoutMs`: default `5000`, clamp `1000` to `60000`.
  - [x] `RetentionDays`: default `30`, clamp `1` to `3650`.
  - [x] `PruneIntervalMinutes`: default `60`, clamp `1` to `1440`.
  - [x] `CollectHostTelemetry`: default `true`.
  - [x] `CollectProviderTelemetry`: default `true`.
  - [x] `PersistRawSnapshots`: default `true`.
  - [x] `MaxRawSnapshotBytes`: default `262144`, clamp `0` to `1048576`.
  - [x] `MaxSamplesPerClientResponse`: default `1`, clamp `1` to `250`.
  - [x] `MaxProviderMetricRowsPerResponse`: default `1000`, clamp `0` to `10000`.
- [x] Update `xeno.json`, `docker/xeno.json`, and `docker/factory/xeno.json` with the new settings.
- [ ] Update dashboard settings defaults in `dashboard/src/App.jsx`.
- [ ] Update settings save/load normalization to preserve casing aliases already used by the dashboard.
- [ ] Mark cadence, enabled state, retention, and raw persistence as restart-required unless a live reload path is explicitly implemented.

Why: Settings own product behavior. A typed settings object also gives tests a small, stable place to verify clamps before the background service exists.

Exit criteria:

- [x] Invalid values below 10 seconds become 10 seconds.
- [x] Invalid values above 300 seconds become 300 seconds.
- [ ] Saved settings round-trip through `/v1.0/api/settings`.
- [ ] Server startup logs include whether telemetry collection is enabled without printing secrets.

## Phase 2: Shared Telemetry Contracts

The server and client must share WebSocket protocol DTOs. Do not deserialize telemetry frames into anonymous payloads or untyped JSON structures.

Implementation status: in progress. Core shared DTO mirror, server API DTO names, WebSocket telemetry frame DTOs, and ID helpers have been added.

Implementation checklist:

- [x] Move or mirror telemetry contracts needed by both server and client into `src/Xeno.Core/Models/Telemetry`.
- [x] Keep client-local persistence-only models in `Xeno.Client` when they are not wire contracts.
- [x] Add host telemetry wire contracts:
  - [x] `ClientTelemetryMetadata`
  - [x] `ClientTelemetrySnapshot`
  - [x] `ClientSystemTelemetry`
  - [x] `ClientCpuTelemetry`
  - [x] `ClientMemoryTelemetry`
  - [x] `ClientDiskTelemetry`
  - [x] `ClientDiskVolumeTelemetry`
  - [x] `ClientGpuTelemetry`
  - [x] `ClientGpuDeviceTelemetry`
  - [x] `ClientTelemetryCollectionStatus`
  - [x] `ClientTelemetryCollectionStatusEnum`
  - [x] `ClientTelemetryGpuProviderEnum`
- [x] Add provider telemetry wire contracts:
  - [x] `ClientProviderTelemetrySnapshot`
  - [x] `ClientProviderTelemetryMetadata`
  - [x] `ClientProviderTelemetryProviderStatus`
  - [x] `ClientProviderTelemetryEndpoint`
  - [x] `ClientProviderTelemetryModel`
  - [x] `ClientProviderTelemetryMetric`
  - [x] `ClientProviderTelemetryLabel`
  - [x] `ClientProviderTelemetryKindEnum`
  - [x] `ClientProviderTelemetryStatusEnum`
  - [x] `ClientProviderTelemetryHealthStatusEnum`
  - [x] `ClientProviderTelemetryMetricEnum`
  - [x] `ClientProviderTelemetryMetricSourceEnum`
- [x] Add server persistence/API response contracts:
  - [x] `ServerClientTelemetrySampleRecord`
  - [x] `ServerClientTelemetryGpuSampleRecord`
  - [x] `ServerClientTelemetrySampleDetail`
  - [x] `ServerClientTelemetrySearchRequest`
  - [x] `ServerClientTelemetrySearchResult`
  - [x] `ServerClientTelemetryRollupRequest`
  - [x] `ServerClientTelemetryRollupBucket`
  - [x] `ServerProviderTelemetrySampleRecord`
  - [x] `ServerProviderTelemetryModelSampleRecord`
  - [x] `ServerProviderTelemetryMetricSampleRecord`
  - [x] `ServerProviderTelemetrySampleDetail`
  - [x] `ServerProviderTelemetrySearchRequest`
  - [x] `ServerProviderTelemetrySearchResult`
  - [x] `ServerProviderTelemetryRollupRequest`
  - [x] `ServerProviderTelemetryRollupBucket`
  - [x] `ServerClientTelemetryLatestResponse`
  - [x] `ServerClientTelemetryCollectionStatusResponse`
- [x] Add ID helper methods/prefixes:
  - [x] `ctel_` for server host telemetry samples.
  - [x] `ctgpu_` for server GPU detail rows.
  - [x] `cptel_` for server provider telemetry samples.
  - [x] `cptmdl_` for server provider telemetry model rows.
  - [x] `cptmet_` for server provider telemetry metric rows.
  - [x] `telreq_` for telemetry collection request/attempt IDs.

Why: Client and server protocol contracts need to be versioned and testable. Pulling shared DTOs into Core prevents drift between WebSocket handling, REST responses, SDKs, and documentation.

Exit criteria:

- [x] Client telemetry services can populate Core wire DTOs without losing fields.
- [x] Server persistence models are separate from raw wire snapshots.
- [x] No new telemetry contract relies on STJ DOM types.

## Phase 3: WebSocket Protocol

Add a small protocol for server-requested telemetry. Keep it separate from inference request frames so telemetry collection cannot consume or complete a pending inference response.

Implementation status: in progress. Server/client frame DTOs, client handling for `server.telemetry.request`, server pending telemetry response tracking, tenant/client/session validation, and acknowledgements have been added.

Frame names:

- [x] `server.telemetry.request`: server asks for host and/or provider telemetry.
- [x] `client.telemetry.snapshot`: client returns current host telemetry.
- [x] `client.providerTelemetry.snapshot`: client returns current provider telemetry.
- [ ] `client.telemetry.history`: optional bounded historical host telemetry response, if requested later.
- [ ] `client.providerTelemetry.history`: optional bounded historical provider telemetry response, if requested later.
- [x] `client.telemetry.error`: client could not collect or return host telemetry.
- [x] `client.providerTelemetry.error`: client could not collect or return provider telemetry.
- [x] `server.telemetry.ack`: server persisted or intentionally ignored telemetry.

`server.telemetry.request` fields:

- [x] `type`
- [x] `requestId`
- [x] `tenantId`
- [x] `clientId`
- [x] `sentUtc`
- [x] `collectHostTelemetry`
- [x] `collectProviderTelemetry`
- [x] `includeRawSnapshot`
- [x] `maxRawSnapshotBytes`
- [x] `maxProviderMetricRows`
- [x] `historyFromUtc` nullable for future bounded backfill.
- [x] `historyToUtc` nullable for future bounded backfill.
- [x] `maxSamples` for future bounded backfill.

Client response rules:

- [x] Client must echo `requestId`, `tenantId`, and `clientId`.
- [x] Tenant must match the authenticated WebSocket tenant.
- [x] Client may return host and provider snapshots in separate frames.
- [x] Client must not include provider credentials, authorization headers, prompts, responses, or unbounded raw Prometheus payloads.
- [x] Client should return `client.telemetry.error` or `client.providerTelemetry.error` for partial failures instead of failing the whole request.
- [x] Server sends `server.telemetry.ack` after it processes each telemetry frame.

Server implementation checklist:

- [x] Add `PendingTelemetryResponse` or a channel-based collection result type separate from `PendingClientResponse`.
- [x] Add a `_PendingTelemetryResponses` dictionary keyed by telemetry `requestId`.
- [x] Add telemetry response handlers in `HandleClientMessageAsync`.
- [x] Validate registered client ID, tenant ID, and WebSocket session ownership before accepting a telemetry frame.
- [x] Use `_Settings.TelemetryCollection.RequestTimeoutMs`, not `_Settings.WebSocket.RequestTimeoutMs`.
- [x] Track partial success: host success, provider success, host error, provider error, persisted row counts, elapsed time.
- [ ] Log collection failures with client ID and tenant ID, but without raw snapshots or provider secrets.

Client implementation checklist:

- [x] Update `src/Xeno.Client/Program.cs` receive loop to parse `WebSocketMessageEnvelope` once and branch on message type.
- [x] Handle `server.telemetry.request`.
- [x] Reuse `ClientTelemetryService` for host snapshots.
- [x] Reuse `ClientProviderTelemetryService` for provider snapshots.
- [ ] If the local persistence background services already have a fresh latest snapshot, decide whether to return it or collect now. Favor collect-now when the server explicitly requests current telemetry, but avoid overlapping provider collection.
- [ ] Send typed error frames when telemetry is disabled, collection fails, or provider telemetry is unavailable.
- [x] Preserve inference `server.request` behavior exactly.
- [ ] Add tests for mixed message handling: heartbeat ack, telemetry request, inference request, telemetry error.

Why: WebSocket is already the authenticated, tenant-bound client channel. A separate telemetry protocol keeps collection safe under concurrent inference load.

Exit criteria:

- [ ] Server can ask one connected client for host telemetry and receive a typed response.
- [ ] Server can ask one connected client for provider telemetry and receive a typed response.
- [ ] A malformed telemetry frame is rejected without closing healthy inference traffic.
- [x] A telemetry timeout does not fail any pending inference request.

## Phase 4: Server Collection Service

Put periodic collection in a service instead of embedding the loop inside `XenoServer.StartAsync`.

Implementation status: partially complete. A server collection loop, connected-client snapshotting, concurrency cap, attempts, request timeouts, and REST-visible status have been added inside `XenoServer`; extraction into the planned service files remains pending.

Implementation checklist:

- [ ] Add `src/Xeno.Server/Services/ClientTelemetryCollectionService.cs`.
- [x] Add `src/Xeno.Server/Services/ClientTelemetryCollectionResult.cs`.
- [x] Add `src/Xeno.Server/Services/ClientTelemetryLatestCache.cs`.
- [x] Start the service after `_Database` and `_Routing` are initialized.
- [x] Stop the service in `XenoServer.Stop()`/`Dispose()`.
- [x] Use `PeriodicTimer`.
- [ ] Collect immediately after startup only if telemetry collection is enabled and clients are already connected; otherwise wait for the next interval.
- [x] Snapshot connected clients under `_ClientConnectionsLock`, then release the lock before sending.
- [x] Limit concurrent telemetry requests so a large fleet cannot create a burst that starves inference. Start with a setting or internal cap such as `MaxConcurrentClientRequests`, default `8`, clamp `1` to `64`.
- [x] Skip clients whose session is disconnected or not registered.
- [x] Avoid overlapping collection cycles. If a prior cycle is still running, record `SkippedOverlap` in collection attempts and try again next interval.
- [x] Persist collection attempts even when no sample is received.
- [x] Prune old telemetry on `PruneIntervalMinutes`.
- [x] Update latest cache only after a successful persisted sample.
- [x] Expose service status through an internal method used by REST status endpoints.

Why: A service boundary gives tests a place to drive cadence, cancellation, timeouts, and persistence without starting the full Watson host.

Exit criteria:

- [x] Enabling/disabling settings changes whether the service sends telemetry requests.
- [x] Collection cadence uses the server clamp.
- [x] Stopping the server cancels in-flight telemetry requests.
- [x] Collection status is visible through REST and dashboard.

## Phase 5: Database Schema And Data Access

Telemetry storage must work in every configured database provider. The schema should be append-heavy, range-query-friendly, and tenant-aware.

Implementation status: in progress. Provider-neutral interfaces and SQLite telemetry tables/indexes/data-access methods have been added; MySQL, PostgreSQL, and SQL Server telemetry schemas/method bindings remain pending.

Add interfaces:

- [x] `src/Xeno.Core/Database/Interfaces/IClientTelemetryMethods.cs`
- [x] `src/Xeno.Core/Database/Interfaces/IClientProviderTelemetryMethods.cs`
- [x] `src/Xeno.Core/Database/Interfaces/IClientTelemetryCollectionAttemptMethods.cs`

Add properties to `DatabaseDriverBase`:

- [x] `public IClientTelemetryMethods ClientTelemetry { get; protected set; }`
- [x] `public IClientProviderTelemetryMethods ClientProviderTelemetry { get; protected set; }`
- [x] `public IClientTelemetryCollectionAttemptMethods ClientTelemetryCollectionAttempts { get; protected set; }`

Host telemetry tables:

- [ ] `client_telemetry_samples`
  - [ ] `id` string primary key.
  - [ ] `tenant_id` string, required.
  - [ ] `client_id` string, required.
  - [ ] `client_name` string.
  - [ ] `machine_name` string.
  - [ ] `collection_request_id` string.
  - [ ] `collected_utc` timestamp, required.
  - [ ] `received_utc` timestamp, required.
  - [ ] `persisted_utc` timestamp, required.
  - [ ] `collection_elapsed_ms` double.
  - [ ] `sample_age_ms_at_receive` double.
  - [ ] `os_description` string.
  - [ ] `architecture` string.
  - [ ] `processor_count` integer.
  - [ ] `cpu_available` boolean.
  - [ ] `cpu_status` string.
  - [ ] `cpu_utilization_percent` double nullable.
  - [ ] `memory_available` boolean.
  - [ ] `memory_status` string.
  - [ ] `memory_total_bytes` bigint nullable.
  - [ ] `memory_used_bytes` bigint nullable.
  - [ ] `memory_available_bytes` bigint nullable.
  - [ ] `memory_utilization_percent` double nullable.
  - [ ] `disk_available` boolean.
  - [ ] `disk_status` string.
  - [ ] `disk_total_bytes` bigint nullable.
  - [ ] `disk_used_bytes` bigint nullable.
  - [ ] `disk_available_bytes` bigint nullable.
  - [ ] `disk_utilization_percent` double nullable.
  - [ ] `disk_read_bytes_per_second` double nullable.
  - [ ] `disk_write_bytes_per_second` double nullable.
  - [ ] `gpu_available` boolean.
  - [ ] `gpu_status` string.
  - [ ] `gpu_provider` string.
  - [ ] `gpu_device_count` integer.
  - [ ] `gpu_average_utilization_percent` double nullable.
  - [ ] `gpu_average_memory_utilization_percent` double nullable.
  - [ ] `gpu_average_temperature_celsius` double nullable.
  - [ ] `gpu_average_power_watts` double nullable.
  - [ ] `gpu_total_memory_bytes` bigint nullable.
  - [ ] `gpu_used_memory_bytes` bigint nullable.
  - [ ] `snapshot_json` text nullable.
  - [ ] `active` boolean.

- [ ] `client_telemetry_gpu_samples`
  - [ ] `id` string primary key.
  - [ ] `sample_id` string, required, foreign key when provider supports it.
  - [ ] `tenant_id` string, required.
  - [ ] `client_id` string, required.
  - [ ] `collected_utc` timestamp, required.
  - [ ] `gpu_uuid` string nullable.
  - [ ] `name` string nullable.
  - [ ] `bus_id` string nullable.
  - [ ] `driver_version` string nullable.
  - [ ] `utilization_percent` double nullable.
  - [ ] `memory_total_bytes` bigint nullable.
  - [ ] `memory_used_bytes` bigint nullable.
  - [ ] `memory_free_bytes` bigint nullable.
  - [ ] `memory_utilization_percent` double nullable.
  - [ ] `temperature_celsius` double nullable.
  - [ ] `power_watts` double nullable.

Provider telemetry tables:

- [ ] `client_provider_telemetry_samples`
  - [ ] `id` string primary key.
  - [ ] `tenant_id` string, required.
  - [ ] `client_id` string, required.
  - [ ] `client_name` string.
  - [ ] `collection_request_id` string.
  - [ ] `provider_name` string.
  - [ ] `api_type` string.
  - [ ] `provider_kind` string.
  - [ ] `endpoint_hash` string.
  - [ ] `endpoint_display` string.
  - [ ] `collected_utc` timestamp, required.
  - [ ] `received_utc` timestamp, required.
  - [ ] `persisted_utc` timestamp, required.
  - [ ] `status` string.
  - [ ] `health_status` string.
  - [ ] `version` string nullable.
  - [ ] `available_model_count` integer.
  - [ ] `loaded_model_count` integer.
  - [ ] `active_requests` double nullable.
  - [ ] `queued_requests` double nullable.
  - [ ] `waiting_requests` double nullable.
  - [ ] `prompt_tokens_per_second` double nullable.
  - [ ] `generation_tokens_per_second` double nullable.
  - [ ] `total_tokens_per_second` double nullable.
  - [ ] `request_rate_per_second` double nullable.
  - [ ] `error_rate_per_second` double nullable.
  - [ ] `latency_p95_ms` double nullable.
  - [ ] `time_to_first_token_p95_ms` double nullable.
  - [ ] `cache_usage_percent` double nullable.
  - [ ] `process_id` integer nullable.
  - [ ] `process_name` string nullable.
  - [ ] `last_error` string nullable.
  - [ ] `snapshot_json` text nullable.
  - [ ] `raw_metrics_truncated` boolean.
  - [ ] `active` boolean.

- [ ] `client_provider_telemetry_models`
  - [ ] `id` string primary key.
  - [ ] `sample_id` string, required.
  - [ ] `tenant_id` string, required.
  - [ ] `client_id` string, required.
  - [ ] `provider_name` string.
  - [ ] `model_name` string.
  - [ ] `family` string nullable.
  - [ ] `quantization` string nullable.
  - [ ] `parameter_size` string nullable.
  - [ ] `context_length` bigint nullable.
  - [ ] `loaded` boolean.
  - [ ] `size_bytes` bigint nullable.
  - [ ] `memory_bytes` bigint nullable.
  - [ ] `expires_utc` timestamp nullable.

- [ ] `client_provider_telemetry_metrics`
  - [ ] `id` string primary key.
  - [ ] `sample_id` string, required.
  - [ ] `tenant_id` string, required.
  - [ ] `client_id` string, required.
  - [ ] `provider_name` string.
  - [ ] `model_name` string nullable.
  - [ ] `metric_name` string.
  - [ ] `metric_kind` string.
  - [ ] `metric_value` double nullable.
  - [ ] `unit` string nullable.
  - [ ] `source` string.
  - [ ] `labels_json` text nullable.

Collection attempts:

- [ ] `client_telemetry_collection_attempts`
  - [ ] `id` string primary key.
  - [ ] `tenant_id` string, required.
  - [ ] `client_id` string, required.
  - [ ] `client_name` string nullable.
  - [ ] `started_utc` timestamp, required.
  - [ ] `completed_utc` timestamp nullable.
  - [ ] `elapsed_ms` double nullable.
  - [ ] `host_requested` boolean.
  - [ ] `provider_requested` boolean.
  - [ ] `host_succeeded` boolean.
  - [ ] `provider_succeeded` boolean.
  - [ ] `status` string.
  - [ ] `error_code` string nullable.
  - [ ] `error_message` string nullable.
  - [ ] `host_sample_id` string nullable.
  - [ ] `provider_sample_count` integer.

Required indexes:

- [ ] `client_telemetry_samples(tenant_id, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, client_id, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, client_name, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, cpu_utilization_percent, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, memory_utilization_percent, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, disk_utilization_percent, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, gpu_average_utilization_percent, collected_utc)`
- [ ] `client_telemetry_samples(tenant_id, gpu_average_memory_utilization_percent, collected_utc)`
- [ ] `client_telemetry_gpu_samples(sample_id)`
- [ ] `client_telemetry_gpu_samples(tenant_id, client_id, collected_utc)`
- [ ] `client_telemetry_gpu_samples(tenant_id, gpu_uuid, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, client_id, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, provider_name, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, provider_kind, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, status, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, health_status, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, endpoint_hash, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, active_requests, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, queued_requests, collected_utc)`
- [ ] `client_provider_telemetry_samples(tenant_id, latency_p95_ms, collected_utc)`
- [ ] `client_provider_telemetry_models(sample_id)`
- [ ] `client_provider_telemetry_models(tenant_id, client_id, model_name)`
- [ ] `client_provider_telemetry_models(tenant_id, provider_name, model_name)`
- [ ] `client_provider_telemetry_metrics(sample_id)`
- [ ] `client_provider_telemetry_metrics(tenant_id, provider_name, metric_name)`
- [ ] `client_provider_telemetry_metrics(tenant_id, client_id, metric_name)`
- [ ] `client_telemetry_collection_attempts(tenant_id, started_utc)`
- [ ] `client_telemetry_collection_attempts(tenant_id, client_id, started_utc)`
- [ ] `client_telemetry_collection_attempts(tenant_id, status, started_utc)`

Data access methods:

- [ ] `CreateHostSampleAsync`
- [ ] `CreateProviderSnapshotAsync`
- [ ] `ReadHostDetailAsync`
- [ ] `ReadProviderDetailAsync`
- [ ] `SearchHostAsync`
- [ ] `SearchProviderAsync`
- [ ] `RollupHostAsync`
- [ ] `RollupProviderAsync`
- [ ] `LatestHostByClientAsync`
- [ ] `LatestProviderByClientAsync`
- [ ] `DeleteHostAsync`
- [ ] `DeleteProviderAsync`
- [ ] `DeleteHostBulkAsync`
- [ ] `DeleteProviderBulkAsync`
- [ ] `DeleteOlderThanAsync`
- [ ] `CreateAttemptAsync`
- [ ] `CompleteAttemptAsync`
- [ ] `SearchAttemptsAsync`

Why: The dashboard, routing engine, retention job, and APIs all need efficient range and latest-sample access. Tables and indexes make the feature operable as data volume grows.

Exit criteria:

- [ ] First startup creates all tables and indexes idempotently.
- [ ] All providers pass schema initialization tests.
- [ ] Inserts and range queries are tenant-scoped.
- [ ] Deleting a host/provider sample removes child rows.

## Phase 6: Server REST API

Expose telemetry as server control-plane APIs. These routes require authentication and tenant visibility checks.

Implementation status: in progress. Canonical REST route handlers for status, latest, collect, search, rollups, detail, delete, attempts, and metrics have been added. OpenAPI schema/detail coverage remains pending.

Canonical routes:

- [x] `GET /v1.0/api/client-telemetry/status`
- [x] `GET /v1.0/api/client-telemetry/latest`
- [x] `POST /v1.0/api/client-telemetry/collect`
- [x] `POST /v1.0/api/client-telemetry/history/search`
- [x] `POST /v1.0/api/client-telemetry/history/rollups`
- [x] `GET /v1.0/api/client-telemetry/history/{id}`
- [x] `DELETE /v1.0/api/client-telemetry/history/{id}`
- [x] `POST /v1.0/api/client-telemetry/history/delete`
- [x] `GET /v1.0/api/client-telemetry/providers/latest`
- [x] `POST /v1.0/api/client-telemetry/providers/collect`
- [x] `POST /v1.0/api/client-telemetry/providers/history/search`
- [x] `POST /v1.0/api/client-telemetry/providers/history/rollups`
- [x] `GET /v1.0/api/client-telemetry/providers/history/{id}`
- [x] `DELETE /v1.0/api/client-telemetry/providers/history/{id}`
- [x] `POST /v1.0/api/client-telemetry/providers/history/delete`
- [x] `GET /v1.0/api/client-telemetry/collection-attempts`
- [x] `GET /v1.0/api/client-telemetry/metrics`

Legacy aliases:

- [ ] Add `/api/v1/clienttelemetry/...` aliases only where they preserve this repository's established alias pattern.

Route behavior:

- [x] `status` returns settings, service running state, next collection estimate, last cycle summary, connected client count, clients eligible for collection, and recent failure count.
- [ ] `latest` supports `tenantId`, `clientId`, and `clientName` filters where authorized.
- [x] `collect` triggers immediate collection for all connected clients or one `clientId`. It returns attempt IDs and does not require waiting for every client unless `wait=true` is explicitly supported.
- [ ] Host `search` supports tenant, client, machine name, time range, metric, metric min/max, paging, sort, and freshness filters.
- [ ] Host `rollups` supports metric, time range, bucket minutes, client filter, and emits empty buckets.
- [ ] Provider `latest` supports client, provider, model, status, and health filters.
- [ ] Provider `search` supports tenant, client, provider, model, status, health status, metric, metric min/max, time range, paging, and sort.
- [ ] Provider `rollups` supports metric, provider/model/client filters, time range, bucket minutes, and emits empty buckets.
- [ ] Detail routes return normalized row data, child rows, collection attempt data, and bounded raw snapshot JSON when persisted.
- [x] Delete routes require delete/admin permission and return `204` for single deletes or `{ "deleted": n }` for bulk deletes.
- [x] `metrics` returns the metric catalog used by routing policies and dashboard selectors.

OpenAPI checklist:

- [x] Add `Client Telemetry` tag.
- [x] Register every route in manual OpenAPI path generation.
- [x] Add request body schemas and examples for search, rollups, collect, and bulk delete.
- [ ] Add response schemas for status, latest, search, rollup, detail, attempts, metric catalog.
- [x] API Explorer names map correctly to operations.

Why: The server dashboard, SDKs, API Explorer, automation scripts, and Postman all need one stable API surface. Routes must also make telemetry auditable and removable.

Exit criteria:

- [x] Every route is visible in `/openapi.json`.
- [ ] API Explorer can execute every route with an authenticated dashboard session.
- [ ] Tenant-scoped users cannot read or delete another tenant's telemetry.

## Phase 7: Server Dashboard UX

Add `CONTROL > Client Telemetry`. The page should use the same visual language as the client local telemetry pages, not a new dashboard style.

Implementation status: in progress. A reachable `CONTROL > Client Telemetry` route, KPI row, status panel, latest host/provider tables, collection attempts table, metric catalog display, refresh, and collect-now action have been added and the dashboard production build passes. Historical analytics charts, detail modals, delete workflows, routing policy editor extensions, and viewport screenshot verification remain pending.

Navigation and information architecture:

- [x] Add a `Client Telemetry` link under the `control` nav group in `dashboard/src/App.jsx`.
- [x] Use a telemetry icon from `lucide-react`, such as `Gauge` or `LineChart`.
- [x] Add page metadata title and description keys.
- [x] Add route `/dashboard/clients/telemetry` or `/dashboard/client-telemetry`. Prefer the path that keeps the sidebar label under `CONTROL`.
- [x] Keep `Clients`, `Advertised Models`, `Routing Policies`, and `Client Telemetry` in a logical order.

Client Telemetry live page:

- [x] KPI row: connected clients, clients with fresh host telemetry, clients with stale telemetry, average CPU, average memory, average GPU utilization, unhealthy provider count.
- [ ] Time range segmented control: Last Hour, Last Day, Last Week, Last Month.
- [ ] Auto-refresh dropdown: None, 15s, 30s, 60s, 180s, 300s.
- [x] Manual refresh icon button.
- [x] Manual collect-now icon button.
- [ ] Filters: tenant where visible, client, metric, provider, model, freshness.
- [x] Host telemetry chart using existing SVG chart patterns and the client dashboard telemetry chart look.
- [x] Provider telemetry chart using the same chart style.
- [ ] Latest host telemetry table with row-click detail modal.
- [ ] Latest provider telemetry table with row-click detail modal.
- [x] Status panel for collection service state and recent collection attempts.

Client Telemetry analytics page or tab:

- [ ] Historical samples table.
- [ ] Provider samples table.
- [x] Collection attempts table.
- [ ] Rollup chart with min/average/max where available.
- [ ] Detail modals use the same `request-details` sizing pattern as existing request history details.
- [ ] JSON copy controls for raw snapshots and detail payloads.
- [ ] Delete action with confirmation.
- [ ] Empty, loading, disabled, stale, unavailable, and error states.

Routing policy UI:

- [ ] Extend `RoutingPolicyEditModal` to edit telemetry conditions.
- [ ] Add metric selector populated from `/v1.0/api/client-telemetry/metrics`.
- [ ] Add filter rows with metric, operator, value type, and value.
- [ ] Add ranking rows with metric, direction, and weight.
- [ ] Add max telemetry age input.
- [ ] Add fallback mode selector.
- [ ] Add validation feedback from the server.
- [ ] Show policy telemetry evidence in request history details.

Style and i18n checklist:

- [x] Use existing cards only for repeated items, tables, and modals.
- [x] Charts use existing CSS variables and dark-mode colors.
- [x] No charting dependency is added.
- [x] All strings are added to `dashboard/src/i18n/resources.js`.
- [ ] Tooltips describe effect, not only label.
- [x] Use locale-aware formatters for timestamps, durations, bytes, percentages, and counts.
- [ ] Validate 1280px, 768px, and 390px viewports.
- [ ] Verify no table controls, action menus, legends, or modal headers overlap.

Why: Operators need a fleet view, not a second local dashboard. Keeping the local dashboard's telemetry look preserves learned behavior while adapting it for multi-client filtering and routing policy workflows.

Exit criteria:

- [ ] `CONTROL > Client Telemetry` is reachable and useful with no samples, stale samples, fresh samples, and provider-only failures.
- [x] Charts visually match the Xeno client local telemetry pages.
- [ ] Routing policy telemetry controls are understandable without reading API docs.

## Phase 8: Routing Policy Conditions

Use Conductor's load-balancing policy pattern as the reference: typed metric catalog, filters, ranking rules, max telemetry age, fallback behavior, and diagnostics. Xeno can implement this inside existing `RoutingPolicy` rather than creating a separate `LoadBalancingPolicy` entity.

Implementation status: partially complete. Routing policy telemetry models/enums, metric catalog route, and policy save/load round-trip fields have been added. Runtime evaluator, latest cache integration, fail-closed behavior, and evidence persistence remain pending.

Model changes:

- [x] Add `RoutingPolicyCondition`.
- [x] Add `RoutingPolicyRankingRule`.
- [x] Add `RoutingPolicyMetricDefinition`.
- [x] Add `RoutingPolicyMetricValue`.
- [x] Add `RoutingPolicyTelemetryEvidence`.
- [x] Add enums:
  - [x] `RoutingPolicyOperatorEnum`: `Equal`, `NotEqual`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`.
  - [x] `RoutingPolicyMetricValueTypeEnum`: `Number`, `Boolean`, `String`.
  - [x] `RoutingPolicyRankingDirectionEnum`: `Ascending`, `Descending`.
  - [x] `RoutingPolicyFallbackModeEnum`: `UseLoadBalancingMode`, `FailClosed`.
- [x] Extend `RoutingPolicy` with:
  - [x] `MaxTelemetryAgeMs`, default `30000`, clamp `1000` to `300000`.
  - [x] `TelemetryFilters`.
  - [x] `TelemetryRanking`.
  - [x] `TelemetryFallbackMode`.
- [x] Extend `RoutingPolicySaveRequest` with the same fields.
- [x] Preserve default `*/*` policy behavior when no telemetry filters/ranking are configured.

Metric catalog:

- [x] `health.connected` boolean.
- [x] `health.hasCapacity` boolean.
- [x] `health.inFlightRequests` number.
- [x] `client.weight` number.
- [x] `client.maxParallelRequests` number.
- [x] `client.heartbeatAgeMs` number.
- [x] `telemetry.ageMs` number.
- [x] `telemetry.host.available` boolean.
- [x] `telemetry.cpu.utilizationPercent` number.
- [x] `telemetry.memory.utilizationPercent` number.
- [x] `telemetry.memory.availableBytes` number.
- [x] `telemetry.disk.utilizationPercent` number.
- [x] `telemetry.disk.readBytesPerSecond` number.
- [x] `telemetry.disk.writeBytesPerSecond` number.
- [x] `telemetry.gpu.available` boolean.
- [x] `telemetry.gpu.deviceCount` number.
- [x] `telemetry.gpu.avgUtilizationPercent` number.
- [x] `telemetry.gpu.avgMemoryUtilizationPercent` number.
- [x] `telemetry.gpu.totalMemoryBytes` number.
- [x] `telemetry.gpu.usedMemoryBytes` number.
- [x] `telemetry.gpu.avgTemperatureCelsius` number.
- [x] `provider.available` boolean.
- [x] `provider.healthStatus` string.
- [x] `provider.activeRequests` number.
- [x] `provider.queuedRequests` number.
- [x] `provider.waitingRequests` number.
- [x] `provider.loadedModelCount` number.
- [x] `provider.latencyP95Ms` number.
- [x] `provider.timeToFirstTokenP95Ms` number.
- [x] `provider.cacheUsagePercent` number.
- [x] `provider.requestRatePerSecond` number.
- [x] `provider.errorRatePerSecond` number.

Routing engine changes:

- [ ] Add `RoutingPolicyEvaluator` or extend `RoutingService` with a dedicated telemetry policy evaluator.
- [ ] Resolve latest host/provider telemetry from `ClientTelemetryLatestCache` first.
- [ ] Fall back to database latest lookup only when the cache is cold.
- [ ] Treat stale telemetry as unavailable for metrics other than `telemetry.ageMs`.
- [ ] If a filter metric is unavailable, remove that client from the candidate set.
- [ ] If a ranking metric is unavailable, remove that client from the candidate set unless fallback mode says to ignore telemetry ranking.
- [ ] If no candidates remain and fallback is `FailClosed`, return `NoEligibleClients` or `PolicyRejected` with a clear reason.
- [ ] If no candidates remain and fallback is `UseLoadBalancingMode`, use the existing policy load-balancing mode and record `PolicyFallbackUsed = true`.
- [ ] Normalize ranking scores per metric across candidates and combine weighted scores.
- [ ] Apply tie-breaker through existing load-balancing mode or a policy-specific tie breaker.
- [ ] Record per-candidate telemetry evidence in `RoutingDecision`.
- [ ] Persist telemetry evidence in request history so operators can explain decisions later.
- [ ] Add trace headers for telemetry policy outcome where useful, without making headers too large.

Validation routes:

- [x] Add `POST /v1.0/api/routing-policies/validate`.
- [x] Add `GET /v1.0/api/routing-policies/metrics` or reuse `/v1.0/api/client-telemetry/metrics`.
- [x] Validate unsupported metrics, wrong value types, unparseable values, non-positive ranking weights, and impossible stale age. Invalid enum payloads are rejected by JSON enum binding before validation.
- [ ] Dashboard create/edit should call validation before save.

Why: Telemetry-based routing is dangerous if it silently does the wrong thing. A metric catalog plus explainable evidence makes failures diagnosable and policy authoring tractable.

Exit criteria:

- [ ] A policy can require GPU availability.
- [ ] A policy can prefer lower GPU utilization.
- [ ] A policy can exclude clients with stale telemetry.
- [ ] A policy can fail closed when telemetry is unavailable.
- [ ] Request history explains which telemetry condition excluded or selected a client.

## Phase 9: Request History And Analytics Integration

Telemetry should show up where operators already debug routing.

Implementation checklist:

- [ ] Extend `RoutingDecision` with telemetry policy ID/name/evidence fields.
- [ ] Extend `RequestHistoryEntry` with:
  - [ ] `HostTelemetrySampleId`
  - [ ] `ProviderTelemetrySampleId`
  - [ ] `TelemetryAgeMs`
  - [ ] `TelemetryPolicyFallbackUsed`
  - [ ] `TelemetryPolicyEvidence`
- [ ] Populate these fields near client selection.
- [ ] Add request-history list filters for telemetry sample ID, stale telemetry, and policy fallback where practical.
- [ ] Add request details sections:
  - [ ] selected client latest host telemetry summary.
  - [ ] selected provider latest telemetry summary.
  - [ ] telemetry policy evidence table.
  - [ ] links/copyable IDs for host/provider telemetry details.
- [ ] Add analytics counters for telemetry freshness failures and policy fallbacks.

Why: Routing changes are only operable when every decision can be reconstructed after the request finishes.

Exit criteria:

- [ ] A failed telemetry policy decision is visible in request history.
- [ ] A successful telemetry-ranked decision shows the selected client's metric values.

## Phase 10: SDKs

SDKs should follow server REST API coverage. They do not need WebSocket collection internals.

Implementation status: in progress. C#, JavaScript, and Python SDK route wrappers for telemetry status/latest/collect/search/rollups/detail/delete/provider/metric catalog have been added and existing SDK tests pass. Dedicated new telemetry SDK tests and typed telemetry model layers remain pending.

C# SDK:

- [ ] Add models for telemetry status, search requests, search results, rollups, details, collect response, and metric catalog.
- [x] Add methods:
  - [x] `GetClientTelemetryStatusAsync`
  - [x] `GetLatestClientTelemetryAsync`
  - [x] `CollectClientTelemetryAsync`
  - [x] `SearchClientTelemetryAsync`
  - [x] `GetClientTelemetryRollupsAsync`
  - [x] `GetClientTelemetryDetailAsync`
  - [x] `DeleteClientTelemetryAsync`
  - [x] `GetLatestClientProviderTelemetryAsync`
  - [x] `SearchClientProviderTelemetryAsync`
  - [x] `GetClientProviderTelemetryRollupsAsync`
  - [x] `GetClientProviderTelemetryDetailAsync`
  - [x] `DeleteClientProviderTelemetryAsync`
  - [x] `GetClientTelemetryMetricCatalogAsync`
- [x] Add tests in `src/sdk/csharp/Xeno.Sdk.Tests`.
- [x] Update `src/sdk/csharp/README.md` and `TESTING.md`.

JavaScript SDK:

- [x] Add equivalent API methods.
- [x] Add request-building tests.
- [x] Update README and TESTING.

Python SDK:

- [x] Add equivalent API methods.
- [x] Add request-building tests.
- [x] Update README and TESTING.

Why: Once telemetry is a server API, automation clients need a stable wrapper. Keeping SDKs current prevents the Postman collection from becoming the only integration guide.

Exit criteria:

- [x] SDK tests prove URL, method, query, and body construction for every telemetry API.
- [x] README examples compile or run in principle against the documented API.

## Phase 11: Documentation

Documentation must describe the actual shipped behavior, not the desired behavior from this plan.

Implementation status: in progress. README, client getting started, WebSocket API, REST API, model routing, and changelog now describe the implemented telemetry settings, WebSocket frames, REST routes, dashboard location, privacy limits, and routing policy fields. Full correctness pass against a live telemetry deployment remains pending.

Update:

- [x] `README.md`: fleet telemetry overview, dashboard location, settings summary, routing policy mention.
- [x] `CLIENT_GETTING_STARTED.md`: client compatibility, local telemetry reuse, WebSocket behavior, no prompt/credential collection.
- [x] `WEBSOCKETS_API.md`: implemented telemetry frames, payload schemas, examples, errors, security notes, and ordering.
- [x] `REST_API.md`: every telemetry route, request, response, query, authorization rule, and example.
- [x] `MODEL_ROUTING.md`: telemetry filters/ranking, metric catalog, fallback behavior, examples, troubleshooting.
- [x] `CHANGELOG.md`: concise feature summary and migration notes.
- [x] SDK READMEs: new methods and examples.
- [ ] Docker docs or comments if telemetry settings appear in Docker sample configs.
- [x] `TELEMETRY_COLLECTION.md`: update status/checklists as work is implemented.

Documentation content requirements:

- [x] Explain enabled/disabled behavior.
- [x] Explain collection frequency and clamp range: minimum 10 seconds, maximum 300 seconds.
- [x] Explain retention and pruning.
- [x] Explain that the server controls collection cadence.
- [x] Explain partial telemetry and unavailable metrics.
- [x] Explain raw snapshot size limits.
- [x] Explain that prompts, responses, credentials, authorization headers, and unbounded raw metrics are not collected.
- [x] Explain dashboard workflow under `CONTROL > Client Telemetry`.
- [ ] Explain routing policy examples:
  - [ ] GPU required.
  - [ ] Prefer lowest GPU utilization.
  - [ ] Avoid high memory pressure.
  - [ ] Prefer providers with shorter queue depth.
  - [ ] Fail closed on stale telemetry.
- [ ] Include troubleshooting steps for no samples, stale telemetry, missing GPU metrics, provider metric gaps, and policy rejection.

Why: This feature crosses client, server, dashboard, APIs, and routing. Incomplete docs will create support load and bad policies.

Exit criteria:

- [ ] A new operator can enable telemetry, verify samples, inspect charts, and create a telemetry-based routing policy using docs alone.

## Phase 12: Postman Collection

Update `Xeno.postman_collection.json` only after route names and request bodies settle.

Implementation status: in progress. Client Telemetry variables and a Client Telemetry folder with status, collection, latest, host/provider search, rollups, detail, delete, bulk delete, and metric catalog requests have been added. Live execution and generated ID capture tests remain pending.

Variables:

- [x] `clientTelemetrySampleId`
- [x] `clientProviderTelemetrySampleId`
- [x] `clientTelemetryClientId`
- [x] `clientTelemetryProviderName`
- [x] `clientTelemetryModelName`
- [ ] `routingPolicyId`

Requests:

- [x] Get client telemetry status.
- [x] Trigger host/provider collection for all clients.
- [x] Trigger collection for one client.
- [x] Get latest host telemetry.
- [x] Search host telemetry.
- [x] Host telemetry rollups.
- [x] Get host telemetry detail.
- [x] Delete host telemetry detail.
- [x] Bulk delete host telemetry by range.
- [x] Get latest provider telemetry.
- [x] Search provider telemetry.
- [x] Provider telemetry rollups.
- [x] Get provider telemetry detail.
- [x] Delete provider telemetry detail.
- [x] Bulk delete provider telemetry by range.
- [x] Get telemetry metric catalog.
- [x] Validate routing policy with telemetry filters.
- [x] Create routing policy with telemetry filters.
- [x] Update routing policy with telemetry ranking.
- [ ] Run an OpenAI-compatible request and inspect telemetry routing headers.

Postman quality requirements:

- [x] Every request has the correct auth headers.
- [x] Every route uses canonical `/v1.0/api/...` unless explicitly testing a legacy alias.
- [x] Example bodies use valid enum values and realistic metrics.
- [ ] Tests capture IDs from create/search responses where practical.
- [x] Destructive requests are grouped and clearly named.

Why: Postman is both an integration harness and a source of truth for operators who do not use SDKs.

Exit criteria:

- [x] The collection imports cleanly.
- [ ] Every telemetry request can be executed in sequence against a running local server with at least one connected client.

## Phase 13: Tests

Use the existing build commands from `AGENTS.md`.

Implementation status: in progress. `dotnet build`, Touchstone automated runner, xUnit, NUnit, dashboard build, dashboard i18n test, C# SDK tests, JavaScript SDK tests, and Python SDK tests pass. New telemetry-specific Touchstone, dashboard Playwright, and SDK assertions remain pending.

Shared Touchstone tests:

- [x] Telemetry collection settings defaults.
- [x] Telemetry collection settings clamps.
- [x] Telemetry ID prefix shape.
- [x] WebSocket telemetry request/response serialization.
- [ ] Client handles `server.telemetry.request`.
- [ ] Server rejects telemetry response with wrong tenant.
- [ ] Server rejects telemetry response from wrong client session.
- [ ] Server telemetry timeout records failed attempt.
- [ ] Server collection service skips disabled settings.
- [ ] Server collection service honors interval clamp.
- [ ] Server collection service does not overlap cycles.
- [ ] SQLite schema creation.
- [ ] MySQL schema creation.
- [ ] PostgreSQL schema creation.
- [ ] SQL Server schema creation.
- [ ] Host telemetry insert/read/detail/search/rollup/delete.
- [ ] Provider telemetry insert/read/detail/search/rollup/delete.
- [ ] Collection attempts insert/search/update.
- [ ] Retention pruning deletes old parent and child rows.
- [x] Latest cache updates after successful persistence.
- [x] Routing policy metric catalog contains required metrics.
- [x] Routing policy validation rejects unsupported metrics.
- [x] Routing policy validation rejects wrong value type.
- [ ] Routing policy filter excludes stale telemetry.
- [ ] Routing policy ranking prefers lower utilization.
- [ ] Routing policy fail-closed returns a clear routing failure.
- [ ] Request history captures telemetry policy evidence.
- [ ] REST route authorization blocks cross-tenant telemetry access.
- [x] OpenAPI includes every telemetry route.

Dashboard tests:

- [x] Build dashboard.
- [x] Verify i18n keys.
- [ ] Playwright smoke test for `CONTROL > Client Telemetry`.
- [ ] Chart renders nonblank SVG data for host telemetry.
- [ ] Chart renders nonblank SVG data for provider telemetry.
- [ ] Empty state when telemetry is disabled.
- [ ] Stale state when latest samples exceed freshness threshold.
- [ ] Manual collect button calls the API.
- [ ] Detail modal opens and includes copy controls.
- [ ] Routing policy modal can add telemetry filter/ranking rows.
- [ ] 1280px, 768px, and 390px screenshots have no overlapping controls.

SDK tests:

- [x] C# SDK telemetry route construction and typed deserialization.
- [x] JavaScript SDK telemetry route construction.
- [x] Python SDK telemetry route construction.

Manual verification commands:

```powershell
dotnet build src/Xeno.sln
dotnet run --project src/Test.Automated -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
npm --prefix dashboard install
npm --prefix dashboard run build
npm --prefix dashboard test
dotnet test src/sdk/csharp/Xeno.Sdk.Tests
npm --prefix src/sdk/javascript test
python -m pytest src/sdk/python/tests
```

Why: The high-risk areas are protocol concurrency, tenant isolation, provider-specific schema, routing behavior, and dashboard correctness. Tests need to cover those directly.

Exit criteria:

- [x] All required builds and tests pass.
- [ ] Any skipped provider-matrix tests have an explicit reason and owner.

Latest verification results:

- [x] `dotnet build src/Xeno.sln` passed after final changes: 0 warnings, 0 errors.
- [x] `dotnet run --project src/Test.Automated -- --results results.json` passed after telemetry and validation tests: 53 passed, 0 failed.
- [x] `dotnet test src/Test.Xunit` passed after telemetry and validation tests: 53 passed, 0 failed.
- [x] `dotnet test src/Test.Nunit` passed after telemetry and validation tests: 53 passed, 0 failed.
- [x] `npm.cmd --prefix dashboard run build` passed after telemetry chart changes.
- [x] `npm.cmd --prefix dashboard test` passed.
- [x] `dotnet test src/sdk/csharp/Xeno.Sdk.Tests` passed after telemetry tests: 7 passed, 0 failed.
- [x] `npm.cmd --prefix src/sdk/javascript test` passed after telemetry tests: 5 passed, 0 failed.
- [x] `python -m pytest src/sdk/python/tests` passed after telemetry tests: 5 passed, 0 failed.
- [x] `Get-Content Xeno.postman_collection.json -Raw | ConvertFrom-Json` passed after telemetry routing policy requests were added.

## Phase 14: Security, Privacy, And Operations

Security checklist:

- [x] WebSocket telemetry frames require the existing client credential authentication.
- [x] Telemetry response tenant must match authenticated WebSocket tenant.
- [x] Telemetry response client ID must match the registered connection.
- [x] Server REST routes enforce authentication and authorization.
- [x] Server never sends telemetry requests to inactive clients.
- [x] Server never persists prompts, responses, provider credentials, auth headers, cookies, `x-secret-key`, `x-token`, or API keys as telemetry.
- [x] Raw snapshots are bounded and optional.
- [x] Error messages include client and tenant context but avoid secrets.
- [x] Delete APIs require delete/admin permission.

Operations checklist:

- [ ] Metrics endpoint exposes collection counters: attempted, succeeded, failed, timed out, skipped, stale samples.
- [ ] Logs include collection cycle start/end summary at debug/info level.
- [ ] Logs include per-client failures at warning level.
- [ ] Retention pruning logs count deleted.
- [ ] Startup logs show telemetry settings after clamp.
- [ ] Backup/restore behavior is explicit. Decide whether telemetry tables are included in backup by default or excluded as time-series operational data.
- [x] Docker sample settings include telemetry settings.
- [x] Large fleet behavior is bounded by concurrency and request timeout.

Why: Telemetry is operationally sensitive even when it avoids payload content. The product should make collection visible, bounded, and easy to disable.

Exit criteria:

- [ ] Operators can prove collection is running or disabled from logs, REST status, and dashboard.
- [ ] Privacy-sensitive data is not present in persisted telemetry tables.

## Phase 15: Documentation And Postman Correctness Pass

Run this as the final planned phase, after tests and implementation stabilize. Treat it as a verification task, not a writing task.

Documentation pass:

- [ ] Read `README.md` and verify every telemetry feature mentioned exists.
- [ ] Read `CLIENT_GETTING_STARTED.md` and verify client behavior matches the implemented WebSocket frames.
- [ ] Read `WEBSOCKETS_API.md` and verify every frame, field, direction, example, timeout, error, and security statement is implemented.
- [ ] Read `REST_API.md` and execute or inspect every documented telemetry API.
- [ ] Read `MODEL_ROUTING.md` and verify every telemetry metric, policy field, fallback mode, example, and troubleshooting note is implemented.
- [ ] Read SDK READMEs and verify every telemetry method exists in that SDK.
- [ ] Read `CHANGELOG.md` and confirm it does not overstate behavior.

Postman pass:

- [ ] Import `Xeno.postman_collection.json`.
- [ ] Start a local Xeno server with telemetry collection enabled.
- [ ] Start at least one authenticated Xeno client.
- [ ] Execute auth requests and store session variables.
- [ ] Execute every Client Telemetry request.
- [ ] Execute every Provider Telemetry request.
- [ ] Execute every routing policy validation/create/update request involving telemetry.
- [ ] Execute one OpenAI-compatible request using a telemetry-conditioned routing policy.
- [x] Confirm Postman examples use valid current route paths, headers, request bodies, and enum values.
- [ ] Confirm generated IDs are captured into variables where later requests depend on them.
- [ ] Confirm destructive requests either target known test records or are clearly isolated.

API correctness pass:

- [ ] Compare `/openapi.json` against `REST_API.md`.
- [ ] Compare `/openapi.json` against Postman route list.
- [ ] Compare SDK method list against REST docs.
- [ ] Compare dashboard API client methods against REST docs.
- [ ] Verify every capability has at least one of: dashboard workflow, SDK method, Postman request, or documented intentional exclusion.

Why: The feature touches too many surfaces for "docs updated" to be enough. The final pass should catch stale route names, incorrect examples, missing auth, and SDK/Postman drift before release.

Exit criteria:

- [ ] Every documented capability has been verified.
- [ ] Every documented API has been verified.
- [ ] Every Postman request for telemetry succeeds or has a documented precondition.

## Suggested Implementation Order

1. [ ] Settings, contracts, and schema.
2. [ ] WebSocket request/response handling for one client.
3. [ ] Persistence for host telemetry and provider telemetry.
4. [ ] Collection service loop and status.
5. [ ] REST API and OpenAPI.
6. [ ] Dashboard `CONTROL > Client Telemetry`.
7. [ ] Routing policy telemetry filters/ranking and diagnostics.
8. [ ] Request history evidence.
9. [ ] SDKs.
10. [ ] Documentation.
11. [ ] Postman.
12. [ ] Full verification and correctness pass.

## Completion Notes

Use this section during implementation.

- [ ] Implementation branch:
- [ ] First working server-to-client telemetry request:
- [ ] First successful host sample persisted:
- [ ] First successful provider sample persisted:
- [x] Dashboard route completed: `CONTROL > Client Telemetry` exists with KPI cards, status, latest host/provider tables, collection attempts, metric catalog, and SVG charts.
- [ ] Routing policy telemetry conditions completed: validation and policy save/load fields are implemented; runtime telemetry evaluator and request-history evidence require a service-boundary decision so routing can access latest telemetry/cache data.
- [ ] Documentation pass completed:
- [ ] Postman pass completed:
- [x] Final verification commands and results: local build/test matrix passed on 2026-06-27. See Phase 13 latest verification results.

Remaining work that is not completed in this pass:

- Cross-database telemetry persistence parity for MySQL, PostgreSQL, and SQL Server.
- Full routing-time telemetry evaluator, trace headers, and request-history evidence. This needs a design choice for how Core `RoutingService` receives server-collected latest telemetry.
- Dashboard historical filters, detail modals, delete workflows, routing policy telemetry editor controls, and Playwright viewport screenshots.
- Live server/client telemetry verification and live Postman execution. These require a running server, authenticated client, and seeded telemetry records.
- Documentation/Postman correctness pass against a live deployment.

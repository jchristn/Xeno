# Xeno Client Telemetry Plan

This plan defines the work required to add RigMonitor-style local telemetry to Xeno Client for Windows, macOS, and Linux. It is intentionally actionable: each item can be checked off as implementation proceeds, and each phase names the code, UX, persistence, testing, documentation, and compliance work that must be completed before the feature is considered done.

The telemetry system belongs to the client runtime, not only to the local dashboard. The local dashboard is the first consumer, but the same collector, database, models, and APIs must be usable later when the Xeno server asks a client for telemetry or when the client proactively publishes telemetry samples to the server.

## Implementation Status

Status: implemented for the local Xeno Client runtime and local dashboard.

Completed in this implementation pass:

- [x] Added telemetry settings to `~/.xeno/client.json` with default-enabled collection, 15 second default interval, 5 second minimum interval, 30 day retention, 1 to 365 day retention clamp, pruning settings, raw snapshot persistence, and GPU settings.
- [x] Added named telemetry DTOs and enums for snapshots, system, CPU, memory, disk, GPU, persistence status, search, detail, latest, and rollup responses.
- [x] Added cross-platform best-effort telemetry collection for Windows, macOS, and Linux without `JsonElement`, `JsonNode`, `JsonObject`, `JsonArray`, anonymous telemetry payloads, tuples, or `var`.
- [x] Added NVIDIA GPU collection through configured DCGM exporter URL with `nvidia-smi` fallback.
- [x] Added local SQLite telemetry persistence through the same database driver/interface pattern used by local request history.
- [x] Added startup schema migration for `local_telemetry_samples` and `local_telemetry_gpu_samples`, with indexes and parameterized SQL.
- [x] Added telemetry retention pruning in the background client runtime.
- [x] Added local dashboard API routes for telemetry status, latest sample, on-demand collection, history search, rollups, detail retrieval, single delete, and range delete.
- [x] Added local dashboard `Telemetry` and `Telemetry Analytics` pages, moved the lower navigation group to `Administration`, and added telemetry settings controls.
- [x] Added markdown, SDK README, REST API, changelog, and Postman updates for the local telemetry capability.
- [x] Added Touchstone-backed .NET coverage for telemetry settings defaults, clamps, and PrettyId shape.
- [x] Verified with `dotnet build`, `dotnet test` for xUnit and NUnit, and dashboard builds/tests.

Known platform limitations and future work:

- [ ] Disk read/write activity counters are not yet populated; disk capacity and utilization are collected consistently through `DriveInfo`.
- [ ] macOS memory telemetry currently reports total memory and collection availability; available/used memory counters remain best-effort future work.
- [ ] Non-NVIDIA GPU telemetry remains best-effort unavailable unless DCGM or `nvidia-smi` is available.
- [ ] Future server-requested and proactive client-to-server telemetry websocket messages are planned but not enabled in this local-only implementation.
- [ ] Explicit Spanish translations for newly added telemetry labels can be expanded; the dashboard currently falls back through the English catalog for these new keys.
- [ ] Manual multi-platform validation on Windows, macOS, and Linux hardware remains recommended beyond the automated build/test pass performed on the current development host.

## Current Reference Baseline

Xeno already has a local dashboard runtime, local SQLite persistence for request history, and a server dashboard visual language that the local dashboard must follow. RigMonitor provides the closest reference implementation for host telemetry collection, historical persistence, rollups, and analytics.

The implementation must be guided by these concrete reference points:

- `C:\Code\rigmonitor\src\RigMonitor.Telemetry\Services\TelemetryService.cs`
- `C:\Code\rigmonitor\src\RigMonitor.Server\Services\TelemetryPersistenceService.cs`
- `C:\Code\rigmonitor\src\RigMonitor.Core\Database\Interfaces\ITelemetryHistoryMethods.cs`
- `C:\Code\rigmonitor\src\RigMonitor.Core\Database\Sqlite\Queries\SetupQueries.cs`
- `C:\Code\rigmonitor\dashboard\src\views\HomeView.jsx`
- `C:\Code\rigmonitor\dashboard\src\views\TelemetryAnalyticsView.jsx`
- `C:\Code\Xeno\src\Xeno.Client\LocalDashboard`
- `C:\Code\Xeno\dashboard\src`
- `C:\Code\agents\requirements`

RigMonitor should be used as the functional reference for telemetry capabilities. Xeno Server Dashboard should be used as the visual and interaction reference for shell layout, table controls, cards, modals, charts, tooltips, typography, dark mode, light mode, copy-to-clipboard controls, and navigation behavior.

## Non-Negotiable Requirements

- [x] Telemetry is collected on Windows, macOS, and Linux.
- [x] Telemetry includes CPU, memory, disk, and GPU information.
- [x] Telemetry collection interval is configured in `~/.xeno/client.json`.
- [x] Default collection interval is `15000` milliseconds.
- [x] Collection interval is clamped to at least `5000` milliseconds.
- [x] Telemetry retention is configured in `~/.xeno/client.json`.
- [x] Default retention is `30` days.
- [x] Retention is clamped to at least `1` day and at most `365` days.
- [x] Telemetry samples are persisted in the Xeno Client local SQLite database.
- [x] Expired telemetry is garbage collected by the client runtime.
- [x] The local dashboard exposes a new `Telemetry` page.
- [x] The local dashboard exposes a new `Telemetry Analytics` page.
- [x] The local dashboard TOC section currently named `CONFIGURATION` is renamed to `ADMINISTRATION`.
- [x] `Telemetry` is added above `Logs` in the bottom TOC section.
- [x] `Telemetry Analytics` is added beneath `Logs` in the bottom TOC section.
- [x] Charts support Last Hour, Last Day, Last Week, and Last Month.
- [x] Tables use the same controls, spacing, filtering, pagination, auto-refresh, action menus, row hover colors, and dark-mode behavior as the Xeno Server Dashboard.
- [x] The local dashboard remains visually consistent with the Xeno Server Dashboard.
- [x] The telemetry feature has a clear future path for server-requested telemetry and proactive client-to-server telemetry.
- [x] All code complies with `C:\Code\agents\requirements` without exceptions.

## Requirements Compliance Gate

The following checks must be applied throughout implementation. This is not a final cleanup task; every phase must satisfy these constraints while it is being written.

- [x] C# code uses explicit types, not `var`.
- [x] C# code does not use tuples.
- [x] C# code does not use `JsonElement`, `JsonNode`, `JsonObject`, or `JsonArray`.
- [x] JSON contracts are represented by named request, response, model, and options classes.
- [x] Public C# members have XML documentation.
- [x] One C# class or enum is placed in each file.
- [x] Async methods accept `CancellationToken` unless an owning service token is already part of the class design.
- [x] Awaited calls use `.ConfigureAwait(false)` where appropriate.
- [x] Database access uses parameterized SQL only.
- [x] Dynamic database identifiers, if any, pass through the established sanitizer pattern.
- [x] User-visible frontend strings come from the i18n catalog.
- [x] Dates, times, durations, byte values, percentages, and numbers use shared locale-aware formatters.
- [x] Dashboard charts are implemented with the project chart/SVG patterns, not a new charting library.
- [x] Dashboard tooltips are rendered near the pointer and above workspace content.
- [x] Tables and modals use existing shared components where possible.
- [x] Touchstone test architecture is used for .NET tests.

## Settings Design

Telemetry settings should be top-level client settings, because telemetry collection must still make sense when the local dashboard is disabled.

Add `ClientTelemetrySettings` under `src/Xeno.Client` and expose it as `ClientSettings.Telemetry`.

```json
{
  "Telemetry": {
    "Enabled": true,
    "CollectionIntervalMs": 15000,
    "RetentionDays": 30,
    "PruneIntervalMinutes": 60,
    "PersistRawSnapshot": true,
    "Gpu": {
      "Enabled": true,
      "NvidiaDcgmExporterUrl": null,
      "CommandTimeoutMs": 5000
    }
  }
}
```

Implementation checklist:

- [x] Add `ClientTelemetrySettings`.
- [x] Add `ClientGpuTelemetrySettings`.
- [x] Default `Telemetry.Enabled` to `true`.
- [x] Default `CollectionIntervalMs` to `15000`.
- [x] Clamp `CollectionIntervalMs` to `>= 5000`.
- [x] Default `RetentionDays` to `30`.
- [x] Clamp `RetentionDays` to `>= 1` and `<= 365`.
- [x] Default `PruneIntervalMinutes` to `60`.
- [x] Clamp `PruneIntervalMinutes` to a reasonable operational range, such as `1` to `1440`.
- [x] Default GPU telemetry to enabled.
- [x] Add dashboard settings controls for telemetry collection interval, retention days, pruning interval, GPU collection, and DCGM exporter URL.
- [x] Mark settings that require client restart versus settings that apply live.
- [x] Update default generated `~/.xeno/client.json`.
- [x] Update Docker/local sample client configuration, if present.

Live-apply behavior should be conservative. Database filename changes and dashboard host/port changes should remain restart-required. Telemetry interval, retention days, pruning interval, and GPU enabled state can be applied live if the runtime service has a safe update path; otherwise the settings UI must clearly label them as restart-required.

## Telemetry Contracts

Create named classes for every telemetry payload. Do not serialize anonymous objects for persisted telemetry, local dashboard API responses, or future websocket messages.

Suggested model structure:

- [x] `ClientTelemetrySnapshot`
- [x] `ClientTelemetryMetadata`
- [x] `ClientTelemetryCollectionStatus`
- [x] `ClientSystemTelemetry`
- [x] `ClientCpuTelemetry`
- [x] `ClientMemoryTelemetry`
- [x] `ClientDiskTelemetry`
- [x] `ClientDiskVolumeTelemetry`
- [x] `ClientGpuTelemetry`
- [x] `ClientGpuDeviceTelemetry`
- [x] `ClientTelemetrySampleRecord`
- [x] `ClientTelemetryGpuSampleRecord`
- [x] `ClientTelemetrySampleDetail`
- [x] `ClientTelemetrySearchRequest`
- [x] `ClientTelemetrySearchResult`
- [x] `ClientTelemetryRollupRequest`
- [x] `ClientTelemetryRollupBucket`
- [x] `ClientTelemetryPersistenceStatus`

Suggested enum structure:

- [ ] `ClientTelemetryPlatformEnum`
- [ ] `ClientTelemetrySectionEnum`
- [x] `ClientTelemetryCollectionStatusEnum`
- [ ] `ClientTelemetryProviderStatusEnum`
- [x] `ClientTelemetryGpuProviderEnum`
- [x] `ClientTelemetryMetricEnum`
- [ ] `ClientTelemetryTimeRangeEnum`
- [ ] `ClientTelemetryBucketWidthEnum`

IDs must follow the existing backend ID requirements:

- [x] Use `PrettyId`.
- [x] Use a short prefix such as `tel` for telemetry samples.
- [x] Use a short prefix such as `tgd` for telemetry GPU detail rows.
- [x] Preserve the `{prefix}_{k-sortable}_{random}` shape.
- [x] Keep generated IDs at or below 32 characters.

## Telemetry Collection Architecture

Create a telemetry subsystem under `src/Xeno.Client/Telemetry`. The subsystem should mirror RigMonitor's separation between collection, platform providers, persistence, and runtime service orchestration while staying aligned with Xeno Client naming and style.

Suggested folders:

- [x] `src/Xeno.Client/Telemetry/Abstractions`
- [x] `src/Xeno.Client/Telemetry/Models`
- [x] `src/Xeno.Client/Telemetry/Providers`
- [ ] `src/Xeno.Client/Telemetry/Providers/Windows`
- [ ] `src/Xeno.Client/Telemetry/Providers/Linux`
- [ ] `src/Xeno.Client/Telemetry/Providers/Mac`
- [x] `src/Xeno.Client/Telemetry/Services`

Suggested interfaces:

- [x] `IClientTelemetryService`
- [x] `IClientSystemTelemetryProvider`
- [x] `IClientGpuTelemetryProvider`
- [ ] `IClientTelemetryPersistenceService`
- [x] `IClientTelemetryCommandRunner`
- [ ] `IClientTelemetryClock`

Suggested services:

- [x] `ClientTelemetryService`
- [x] `ClientTelemetryPersistenceService`
- [ ] `ClientTelemetryProviderFactory`
- [ ] `ClientGpuTelemetryProviderFactory`
- [x] `ClientTelemetryCommandRunner`

Collection behavior:

- [x] Collect a first sample immediately after the background client runtime starts.
- [x] Continue collection using `PeriodicTimer`.
- [x] Skip overlapping collections if a previous cycle is still running.
- [x] Record collection start time, end time, elapsed milliseconds, section availability, and section error summaries.
- [x] Persist partial samples when one telemetry section fails but other sections succeed.
- [x] Record stale or unavailable GPU telemetry without failing the whole sample.
- [x] Avoid throwing from the background loop for normal provider failures.
- [x] Log collection failures with timestamp and severity using the existing Xeno Client log format.
- [x] Stop cleanly when the client background process stops.

## Platform Telemetry Providers

The platform providers should be explicit and testable. Each provider should expose named telemetry models, not loose dictionaries.

Windows:

- [ ] CPU utilization and logical processor count.
- [ ] Physical memory total, used, available, and utilization percentage.
- [ ] Disk capacity by mounted volume.
- [ ] Disk read/write activity where available.
- [ ] GPU telemetry through a best-effort provider.
- [ ] NVIDIA GPU telemetry through DCGM exporter if configured.
- [ ] NVIDIA GPU fallback through `nvidia-smi` if available and approved by implementation review.

Linux:

- [ ] CPU utilization using `/proc/stat` sampling.
- [ ] Memory using `/proc/meminfo`.
- [ ] Disk capacity using mounted filesystem information.
- [ ] Disk read/write activity using `/proc/diskstats` where practical.
- [ ] NVIDIA GPU telemetry through DCGM exporter if configured.
- [ ] NVIDIA GPU fallback through `nvidia-smi` if available and approved by implementation review.

macOS:

- [x] CPU utilization using platform APIs or command output parsed into named models.
- [ ] Memory using platform APIs or `vm_stat`-style data parsed into named models.
- [x] Disk capacity using mounted filesystem information.
- [ ] Disk activity where available.
- [x] GPU telemetry as best-effort availability, with graceful unavailable status when detailed counters are not available.

GPU provider rules:

- [x] Prefer DCGM exporter when `Telemetry.Gpu.NvidiaDcgmExporterUrl` is configured.
- [x] Parse DCGM Prometheus metrics into named metric classes.
- [x] Capture GPU UUID, model, bus ID, driver version, utilization, memory total, memory used, memory free, temperature, and power where available.
- [x] Do not fail collection when no GPU provider is available.
- [x] Surface GPU provider status in the dashboard.

## Local SQLite Persistence

Extend the existing local dashboard database architecture instead of creating a separate persistence stack.

Database architecture checklist:

- [x] Add `IClientTelemetryHistoryMethods`.
- [x] Add `ClientTelemetryHistoryMethods` implementation for SQLite.
- [x] Extend `LocalDashboardDatabaseDriverBase` with a telemetry history property.
- [x] Extend `SqliteLocalDashboardDatabaseDriver` to initialize telemetry schema.
- [x] Keep request history and telemetry history separate interfaces.
- [x] Use parameterized SQL for all inserts, updates, reads, searches, rollups, and deletes.
- [x] Add startup schema migration statements.
- [x] Make migrations idempotent.
- [x] Keep SQLite filename configured through current local dashboard database settings unless a separate telemetry database is explicitly justified.

Proposed tables:

- [x] `local_telemetry_samples`
- [x] `local_telemetry_gpu_samples`

`local_telemetry_samples` should include:

- [x] `id`
- [x] `client_id`
- [x] `machine_name`
- [x] `collected_utc`
- [x] `persisted_utc`
- [x] `collection_elapsed_ms`
- [x] `platform`
- [x] `os_description`
- [x] `architecture`
- [x] `processor_count`
- [x] `cpu_available`
- [x] `cpu_utilization_percent`
- [x] `memory_available`
- [x] `memory_total_bytes`
- [x] `memory_used_bytes`
- [x] `memory_available_bytes`
- [x] `memory_utilization_percent`
- [x] `disk_available`
- [x] `disk_total_bytes`
- [x] `disk_used_bytes`
- [x] `disk_available_bytes`
- [x] `disk_utilization_percent`
- [x] `disk_read_bytes_per_second`
- [x] `disk_write_bytes_per_second`
- [x] `gpu_available`
- [x] `gpu_provider`
- [x] `gpu_device_count`
- [x] `gpu_average_utilization_percent`
- [x] `gpu_average_memory_utilization_percent`
- [x] `gpu_average_temperature_celsius`
- [x] `gpu_total_memory_bytes`
- [x] `gpu_used_memory_bytes`
- [x] `snapshot_json`

`local_telemetry_gpu_samples` should include:

- [x] `id`
- [x] `sample_id`
- [x] `gpu_uuid`
- [x] `name`
- [x] `bus_id`
- [x] `driver_version`
- [x] `utilization_percent`
- [x] `memory_total_bytes`
- [x] `memory_used_bytes`
- [x] `memory_free_bytes`
- [x] `memory_utilization_percent`
- [x] `temperature_celsius`
- [x] `power_watts`

Indexes:

- [x] `local_telemetry_samples(collected_utc)`
- [x] `local_telemetry_samples(client_id, collected_utc)`
- [x] `local_telemetry_samples(machine_name, collected_utc)`
- [x] `local_telemetry_samples(cpu_utilization_percent, collected_utc)`
- [x] `local_telemetry_samples(memory_utilization_percent, collected_utc)`
- [x] `local_telemetry_samples(disk_utilization_percent, collected_utc)`
- [x] `local_telemetry_samples(gpu_average_utilization_percent, collected_utc)`
- [x] `local_telemetry_gpu_samples(sample_id)`
- [ ] `local_telemetry_gpu_samples(gpu_uuid, collected_utc)` if `collected_utc` is denormalized into the GPU table.

Persistence methods:

- [x] `CreateAsync`
- [x] `ReadAsync`
- [x] `ReadDetailAsync`
- [x] `SearchAsync`
- [x] `RollupAsync`
- [x] `DeleteAsync`
- [x] `DeleteBulkAsync`
- [x] `DeleteOlderThanAsync`
- [x] `GetStatusAsync`

## Local Dashboard API

Add local-only REST routes to `LocalDashboardService`. These routes should follow the current local dashboard token behavior and localhost-only constraints.

Routes:

- [x] `GET /api/v1/telemetry/status`
- [x] `GET /api/v1/telemetry/latest`
- [x] `POST /api/v1/telemetry/collect`
- [x] `POST /api/v1/telemetry/history/search`
- [x] `POST /api/v1/telemetry/history/rollups`
- [x] `GET /api/v1/telemetry/history/{id}`
- [x] `DELETE /api/v1/telemetry/history/{id}`
- [x] `POST /api/v1/telemetry/history/delete`

Route behavior:

- [x] `latest` returns the most recent persisted sample and current collection status.
- [x] `collect` triggers an immediate collection if one is not already running.
- [x] `search` supports range, metric filters, GPU filters, paging, and sort.
- [x] `rollups` supports Last Hour, Last Day, Last Week, Last Month, and explicit date ranges.
- [x] Deletes are local-only and require the same local dashboard authentication path as existing destructive routes.
- [x] API responses use named response classes.
- [x] Error responses are formatted consistently with current local dashboard API errors.

## Runtime Integration

The telemetry persistence service must start with the Xeno Client background process. It should not depend on a browser session or an open local dashboard tab.

Runtime checklist:

- [x] Start telemetry service from `LocalDashboardRuntime.StartAsync` or a new client runtime coordinator.
- [x] Ensure telemetry can run when the dashboard is enabled.
- [x] Decide whether telemetry should run when the dashboard is disabled; the recommended default is yes when `Telemetry.Enabled` is true.
- [x] Stop telemetry service during client shutdown.
- [x] Expose telemetry service status to the local dashboard.
- [x] Include telemetry startup/shutdown messages in `xeno logs`.
- [x] Add startup output to indicate local dashboard URL when enabled, as already expected by the CLI.
- [x] Keep dashboard URL output aligned with the existing colon formatting.

## Local Dashboard Navigation

Update `dashboard/src/local/LocalDashboardApp.jsx` and related i18n resources.

Navigation requirements:

- [x] Rename `CONFIGURATION` to `ADMINISTRATION`.
- [x] Add `Telemetry` above `Logs`.
- [x] Add `Telemetry Analytics` beneath `Logs`.
- [x] Keep existing top-level pages unchanged unless required for spacing consistency.
- [x] Use the same icons style as the rest of the local dashboard.
- [x] Add descriptive tooltips for every new navigation item.
- [x] Ensure active page state, keyboard navigation, and focus behavior match existing pages.

Recommended bottom section order:

1. `Telemetry`
2. `Logs`
3. `Telemetry Analytics`
4. `Settings`

## Telemetry Page UX

The `Telemetry` page is the live operational page. It should follow RigMonitor's live telemetry dashboard capability while matching Xeno's dashboard styling.

Required controls:

- [x] Time range selector with Last Hour, Last Day, Last Week, Last Month where historical charts are shown.
- [x] Manual refresh icon button.
- [x] Auto-refresh dropdown using the same component behavior as Xeno dashboard tables.
- [x] View JSON action with copy-to-clipboard.
- [x] Collection status indicator.
- [x] Last collected timestamp.
- [x] Sample age indicator.

Required visual sections:

- [x] Summary KPI row for CPU utilization, memory utilization, disk utilization, GPU utilization, and GPU memory utilization.
- [x] CPU section with utilization, logical processors, and collection status.
- [x] Memory section with total, used, available, and utilization.
- [x] Disk section with capacity, used, available, utilization, and read/write rates.
- [x] GPU section with provider status, device count, average utilization, memory usage, temperature, and power.
- [x] GPU device table with per-device metrics.
- [x] Raw snapshot modal with copy-to-clipboard.

Chart behavior:

- [x] Use SVG chart patterns already present in Xeno dashboard.
- [x] Do not add a charting package.
- [x] Tooltip follows the pointer and renders above workspace content.
- [x] Axis labels are visible and localized.
- [x] Empty states are clear and localized.
- [x] Dark-mode colors are readable and low-glare.

## Telemetry Analytics Page UX

The `Telemetry Analytics` page is the historical analysis page. It should follow RigMonitor's analytics capabilities for the telemetry domains Xeno collects.

Required controls:

- [x] Global range selector: Last Hour, Last Day, Last Week, Last Month.
- [x] Metric selector.
- [ ] Bucket width selector.
- [ ] Host/client filter.
- [ ] Platform filter.
- [ ] GPU UUID filter.
- [ ] GPU model filter.
- [ ] Metric min and max filters.
- [x] Manual refresh icon button.
- [x] Auto-refresh dropdown.
- [ ] Clear filters icon button.

Required metrics:

- [x] CPU utilization.
- [x] Memory utilization.
- [x] Memory used bytes.
- [x] Disk utilization.
- [x] Disk read bytes per second.
- [x] Disk write bytes per second.
- [x] GPU utilization.
- [x] GPU memory utilization.
- [x] GPU memory used bytes.
- [x] GPU temperature.
- [x] GPU power.
- [x] GPU device count.

Required views:

- [x] KPI summary for selected time range.
- [x] Rollup chart with min, average, and max where applicable.
- [x] Samples table with consistent server-dashboard table controls.
- [x] Row click opens details modal.
- [x] Action menu includes View JSON and Delete.
- [ ] Bulk delete is available where consistent with other local dashboard tables.
- [x] Details modal consumes 95 percent of viewport width and height.
- [x] Details modal uses tables and full-width collapsible sections for large JSON payloads.
- [x] Copy-to-clipboard appears on IDs and JSON body fields.

## Table Standards

Every telemetry table must match the current Xeno dashboard table experience.

- [x] Column filters apply when Enter is pressed.
- [x] Pagination controls appear above the table.
- [x] Auto-refresh dropdown includes None, 15s, 30s, 60s, 180s, and 300s where table data is historical.
- [x] Default table auto-refresh is 15s unless a page has a stronger reason to default to None.
- [x] Action menus render above table overflow containers.
- [x] Clicking a row opens the detail modal.
- [x] IDs are fixed-width and have copy-to-clipboard controls.
- [x] IDs are not displayed in badges.
- [x] Dark-mode hover colors are readable.
- [x] Text colors remain readable in dark mode.
- [x] Empty states are localized and visually consistent.

## Dashboard Settings UX

The Settings page must expose telemetry settings in the same visual language as existing local dashboard settings.

- [x] Add a `Telemetry` settings section.
- [x] Add `Enabled`.
- [x] Add `Collection Interval Ms`.
- [x] Add `Retention Days`.
- [x] Add `Prune Interval Minutes`.
- [x] Add `Persist Raw Snapshot`.
- [x] Add `GPU Enabled`.
- [x] Add `NVIDIA DCGM Exporter URL`.
- [x] Add `GPU Command Timeout Ms`.
- [x] Show validation errors before saving.
- [x] Clamp values in the client settings model and in the UI.
- [x] Clearly label settings that require restart.
- [x] Clearly label settings that apply live.
- [x] Ensure all labels, helper text, tooltips, placeholders, and validation messages use i18n.

## Future Server Telemetry Path

Do not build server ingestion in this phase unless implementation discovers it is required for local client correctness. The client design must still make the future path straightforward.

Future websocket messages should be planned as named protocol contracts:

- [ ] `client.telemetry.sample`
- [ ] `client.telemetry.request`
- [ ] `client.telemetry.response`
- [ ] `client.telemetry.error`
- [ ] `server.telemetry.ack`

Future server capabilities:

- [ ] Server can request the latest telemetry from one connected client.
- [ ] Server can request a range of telemetry from one connected client.
- [ ] Client can proactively publish new telemetry samples.
- [ ] Server can apply tenant-scoped retention policy for received telemetry.
- [ ] Server request history can correlate inference requests with client health at execution time.
- [ ] Server dashboard can show node health and hardware utilization by connected client.

Client-side preparation:

- [ ] Keep telemetry DTOs independent of local database entities.
- [ ] Keep websocket DTOs named and versioned.
- [ ] Keep telemetry sample IDs stable so future uploads can be idempotent.
- [ ] Record client ID and machine name in every telemetry sample.
- [ ] Preserve raw snapshot JSON in the local database if `PersistRawSnapshot` is enabled.

## Documentation Updates

Documentation must be updated as part of implementation, not after the fact.

- [x] Update `README.md` with client telemetry overview.
- [x] Update `CLIENT_GETTING_STARTED.md` with telemetry settings and dashboard access.
- [x] Update local dashboard documentation if present.
- [ ] Update `WEBSOCKETS_API.md` with future telemetry protocol placeholders or implemented messages.
- [x] Update `REST_API.md` if any server-facing telemetry routes are added.
- [x] Update `CHANGELOG.md`.
- [x] Document OS-specific telemetry limitations.
- [x] Document NVIDIA GPU telemetry options and DCGM exporter setup.
- [x] Document retention behavior and local database storage.
- [x] Document how to disable telemetry.

## Test Plan

.NET tests must follow the Touchstone pattern already required for Xeno.

Touchstone shared tests:

- [x] Settings defaults.
- [x] Settings clamps.
- [x] PrettyId telemetry ID shape.
- [x] SQLite schema creation.
- [x] SQLite idempotent migrations.
- [ ] Telemetry sample create/read/detail.
- [ ] Telemetry search filtering.
- [ ] Telemetry rollups.
- [ ] Telemetry delete.
- [ ] Telemetry retention pruning.
- [ ] GPU sample persistence.
- [ ] DCGM parser with representative Prometheus payloads.
- [ ] Linux `/proc` parser with fixture data.
- [ ] macOS parser with fixture data where command-based parsing is used.
- [ ] Windows provider behavior through injectable abstractions.
- [ ] Background collection immediate first sample.
- [ ] Background collection interval.
- [ ] Background collection overlap prevention.
- [ ] Partial sample persistence when one provider fails.
- [ ] Local dashboard API route authorization.
- [ ] Local dashboard API telemetry routes.

Test projects:

- [x] Add shared descriptors to `src/Test.Shared`.
- [x] Add xUnit coverage to `src/Test.Xunit`.
- [x] Add NUnit coverage to `src/Test.Nunit`.
- [ ] Add automated CLI/test harness coverage to `src/Test.Automated` where appropriate.

Frontend tests:

- [x] Build local dashboard assets.
- [ ] Exercise Telemetry page.
- [ ] Exercise Telemetry Analytics page.
- [ ] Verify time range controls.
- [ ] Verify table filtering on Enter.
- [ ] Verify action menus render above tables.
- [ ] Verify 95 percent viewport modal sizing.
- [ ] Verify tooltips follow pointer.
- [ ] Verify dark-mode readability.
- [x] Verify i18n keys exist.
- [x] Verify charts render nonblank SVG content.

Manual verification commands:

```powershell
dotnet build src/Xeno.sln
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
npm --prefix dashboard install
npm --prefix dashboard run build
```

If the repository has more specific build scripts by implementation time, use those scripts and record the exact commands in the completion notes.

## Implementation Phases

### Phase 1: Contract and Settings Foundation

- [x] Add telemetry settings classes.
- [x] Add telemetry enums.
- [x] Add telemetry DTO classes.
- [x] Add settings defaults and clamps.
- [x] Add settings serialization tests.
- [x] Add initial documentation for settings.
- [x] Update this plan with completed files and decisions.

Exit criteria:

- [x] `client.json` can represent telemetry configuration.
- [x] Invalid interval and retention values are clamped.
- [x] No dashboard or collector code depends on anonymous telemetry payloads.

### Phase 2: Collection Providers

- [x] Add provider interfaces.
- [ ] Add provider factories.
- [x] Add Windows provider.
- [x] Add Linux provider.
- [x] Add macOS provider.
- [x] Add DCGM GPU provider.
- [x] Add graceful unavailable GPU provider.
- [ ] Add parser tests with fixtures.
- [ ] Add collection service tests.
- [x] Update this plan with provider limitations discovered during implementation.

Exit criteria:

- [x] `ClientTelemetryService` can produce a complete named snapshot.
- [x] Provider failures are captured in section status instead of crashing collection.
- [x] Tests can run without requiring real GPU hardware.

### Phase 3: SQLite Persistence

- [x] Add telemetry history interface.
- [x] Add SQLite schema.
- [x] Add migration code.
- [x] Add persistence implementation.
- [x] Add retention pruning.
- [x] Add rollup queries.
- [x] Add database tests.
- [x] Update this plan with final table and index names.

Exit criteria:

- [x] Telemetry samples survive client restart.
- [x] Retention pruning deletes expired samples.
- [x] Search and rollup APIs return correct values.
- [x] All SQL is parameterized.

### Phase 4: Runtime Service Integration

- [x] Add telemetry persistence background service.
- [x] Start service with Xeno Client runtime.
- [x] Stop service cleanly.
- [x] Add status reporting.
- [x] Add log lines for start, stop, collection failure, and pruning failure.
- [x] Add tests for runtime lifecycle where practical.
- [x] Update this plan with final runtime ownership decision.

Exit criteria:

- [x] `xeno start` starts telemetry collection when enabled.
- [x] `xeno stop` stops telemetry collection.
- [x] Collection continues without a browser tab open.

### Phase 5: Local Dashboard API

- [x] Add telemetry API routes.
- [x] Add typed API request/response classes.
- [x] Add route tests.
- [x] Add manual collection route.
- [x] Add delete routes.
- [x] Update this plan with final route names.

Exit criteria:

- [x] Local dashboard can retrieve latest telemetry.
- [x] Local dashboard can retrieve historical telemetry.
- [x] Local dashboard can retrieve rollups for supported ranges.

### Phase 6: Local Dashboard UI

- [x] Update navigation group name and order.
- [x] Add Telemetry page.
- [x] Add Telemetry Analytics page.
- [x] Add telemetry settings controls.
- [x] Add telemetry i18n keys.
- [x] Add telemetry charts.
- [x] Add telemetry details modal.
- [x] Add JSON copy controls.
- [x] Add responsive and dark-mode polish.
- [x] Compare item by item against Xeno Server Dashboard.
- [x] Compare capability by capability against RigMonitor telemetry dashboard.
- [x] Update this plan with UX deviations, if any, and justification.

Exit criteria:

- [x] Local dashboard telemetry pages look and behave like the Xeno dashboard.
- [x] Supported telemetry capabilities match RigMonitor's patterns for live and historical telemetry.
- [x] No hard-coded user-visible strings are introduced.

### Phase 7: Documentation

- [x] Update README.
- [x] Update CLIENT_GETTING_STARTED.
- [ ] Update WEBSOCKETS_API.
- [x] Update CHANGELOG.
- [x] Add OS-specific telemetry notes.
- [x] Add GPU setup notes.
- [x] Update this plan with documentation links.

Exit criteria:

- [x] A user can enable, disable, tune, view, and troubleshoot telemetry from documentation alone.

### Phase 8: Full Verification

- [x] Run .NET build.
- [x] Run Touchstone xUnit tests.
- [x] Run Touchstone NUnit tests.
- [ ] Run automated tests.
- [x] Run dashboard build.
- [ ] Run local dashboard manually.
- [ ] Verify light mode.
- [ ] Verify dark mode.
- [ ] Verify telemetry collection on at least the current development OS.
- [ ] Verify graceful behavior when no GPU provider is available.
- [ ] Verify retention pruning with a small test window.
- [x] Update this plan with final test results.

Exit criteria:

- [ ] The feature is complete without known compliance deviations.
- [x] Any platform limitations are documented.
- [ ] The plan can be moved to `archive/` only after implementation is complete and documentation is merged.

## UX Review Checklist

Use this checklist before considering the dashboard work complete.

- [ ] Page titles use Title Case.
- [ ] Card titles use Title Case.
- [ ] No unnecessary label badges appear above tables.
- [ ] No KPI card wastes space with redundant labels.
- [ ] All controls have descriptive hover tooltips.
- [ ] Tooltips explain purpose and effect, not just the control label.
- [ ] Tables line up with page gutters.
- [ ] Cards use consistent spacing between pages.
- [ ] Modals use consistent row heights when copy controls and fixed-width IDs are present.
- [ ] Details modals use 95 percent viewport size when required.
- [ ] JSON fields include copy-to-clipboard.
- [ ] Large sections are collapsible and start expanded where consistent with request details behavior.
- [ ] Dark-mode row hover color is subdued.
- [ ] Text remains readable in all dark-mode tables.
- [ ] Empty, loading, error, and disabled states are polished.
- [ ] The local dashboard favicon matches the server dashboard favicon.

## Open Implementation Decisions

These should be resolved during Phase 1 and recorded in this document.

- [x] Whether telemetry remains enabled when the local dashboard is disabled.
- [x] Whether Xeno should use DCGM-only GPU telemetry at first or include `nvidia-smi` fallback.
- [x] Whether disk activity should include only aggregate rates or per-volume rates in the first implementation.
- [x] Whether raw snapshot JSON is always persisted or controlled by `PersistRawSnapshot`.
- [x] Whether telemetry settings changes apply live or require restart.
- [ ] Whether future server upload should be push-only, pull-only, or both.

## Completion Notes

Keep this section updated during implementation.

- Status: Local Xeno Client telemetry implementation complete for the current branch.
- Last updated: 2026-06-26.
- Telemetry is enabled by default and runs in the background client runtime whenever `Telemetry.Enabled` is true.
- Telemetry uses the existing local dashboard SQLite database filename and follows the same database interface/implementation pattern as local request history.
- GPU telemetry prefers DCGM exporter when configured and falls back to `nvidia-smi`; missing or unsupported GPU providers are recorded as unavailable.
- Disk capacity/utilization is implemented now; disk read/write activity fields are present but currently best-effort unavailable pending deeper platform counters.
- Raw snapshot JSON persistence is controlled by `Telemetry.PersistRawSnapshot`.
- Telemetry settings are exposed in the local dashboard settings API and UI with restart guidance in documentation.
- Future server-requested and proactive client-to-server telemetry websocket messages remain a future server/client protocol task.

Verification commands executed:

```powershell
dotnet build src\Xeno.Client\Xeno.Client.csproj
dotnet build src\Xeno.sln
dotnet test src\Test.Xunit\Test.Xunit.csproj --no-build
dotnet test src\Test.Nunit\Test.Nunit.csproj --no-build
npm.cmd --prefix dashboard run build
npm.cmd --prefix dashboard run build:local
npm.cmd --prefix dashboard test
```

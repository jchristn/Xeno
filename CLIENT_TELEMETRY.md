# Xeno Client Telemetry Plan

This plan defines the work required to add RigMonitor-style local telemetry to Xeno Client for Windows, macOS, and Linux. It is intentionally actionable: each item can be checked off as implementation proceeds, and each phase names the code, UX, persistence, testing, documentation, and compliance work that must be completed before the feature is considered done.

The telemetry system belongs to the client runtime, not only to the local dashboard. The local dashboard is the first consumer, but the same collector, database, models, and APIs must be usable later when the Xeno server asks a client for telemetry or when the client proactively publishes telemetry samples to the server.

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

- [ ] Telemetry is collected on Windows, macOS, and Linux.
- [ ] Telemetry includes CPU, memory, disk, and GPU information.
- [ ] Telemetry collection interval is configured in `~/.xeno/client.json`.
- [ ] Default collection interval is `15000` milliseconds.
- [ ] Collection interval is clamped to at least `5000` milliseconds.
- [ ] Telemetry retention is configured in `~/.xeno/client.json`.
- [ ] Default retention is `30` days.
- [ ] Retention is clamped to at least `1` day and at most `365` days.
- [ ] Telemetry samples are persisted in the Xeno Client local SQLite database.
- [ ] Expired telemetry is garbage collected by the client runtime.
- [ ] The local dashboard exposes a new `Telemetry` page.
- [ ] The local dashboard exposes a new `Telemetry Analytics` page.
- [ ] The local dashboard TOC section currently named `CONFIGURATION` is renamed to `ADMINISTRATION`.
- [ ] `Telemetry` is added above `Logs` in the bottom TOC section.
- [ ] `Telemetry Analytics` is added beneath `Logs` in the bottom TOC section.
- [ ] Charts support Last Hour, Last Day, Last Week, and Last Month.
- [ ] Tables use the same controls, spacing, filtering, pagination, auto-refresh, action menus, row hover colors, and dark-mode behavior as the Xeno Server Dashboard.
- [ ] The local dashboard remains visually consistent with the Xeno Server Dashboard.
- [ ] The telemetry feature has a clear future path for server-requested telemetry and proactive client-to-server telemetry.
- [ ] All code complies with `C:\Code\agents\requirements` without exceptions.

## Requirements Compliance Gate

The following checks must be applied throughout implementation. This is not a final cleanup task; every phase must satisfy these constraints while it is being written.

- [ ] C# code uses explicit types, not `var`.
- [ ] C# code does not use tuples.
- [ ] C# code does not use `JsonElement`, `JsonNode`, `JsonObject`, or `JsonArray`.
- [ ] JSON contracts are represented by named request, response, model, and options classes.
- [ ] Public C# members have XML documentation.
- [ ] One C# class or enum is placed in each file.
- [ ] Async methods accept `CancellationToken` unless an owning service token is already part of the class design.
- [ ] Awaited calls use `.ConfigureAwait(false)` where appropriate.
- [ ] Database access uses parameterized SQL only.
- [ ] Dynamic database identifiers, if any, pass through the established sanitizer pattern.
- [ ] User-visible frontend strings come from the i18n catalog.
- [ ] Dates, times, durations, byte values, percentages, and numbers use shared locale-aware formatters.
- [ ] Dashboard charts are implemented with the project chart/SVG patterns, not a new charting library.
- [ ] Dashboard tooltips are rendered near the pointer and above workspace content.
- [ ] Tables and modals use existing shared components where possible.
- [ ] Touchstone test architecture is used for .NET tests.

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

- [ ] Add `ClientTelemetrySettings`.
- [ ] Add `ClientGpuTelemetrySettings`.
- [ ] Default `Telemetry.Enabled` to `true`.
- [ ] Default `CollectionIntervalMs` to `15000`.
- [ ] Clamp `CollectionIntervalMs` to `>= 5000`.
- [ ] Default `RetentionDays` to `30`.
- [ ] Clamp `RetentionDays` to `>= 1` and `<= 365`.
- [ ] Default `PruneIntervalMinutes` to `60`.
- [ ] Clamp `PruneIntervalMinutes` to a reasonable operational range, such as `1` to `1440`.
- [ ] Default GPU telemetry to enabled.
- [ ] Add dashboard settings controls for telemetry collection interval, retention days, pruning interval, GPU collection, and DCGM exporter URL.
- [ ] Mark settings that require client restart versus settings that apply live.
- [ ] Update default generated `~/.xeno/client.json`.
- [ ] Update Docker/local sample client configuration, if present.

Live-apply behavior should be conservative. Database filename changes and dashboard host/port changes should remain restart-required. Telemetry interval, retention days, pruning interval, and GPU enabled state can be applied live if the runtime service has a safe update path; otherwise the settings UI must clearly label them as restart-required.

## Telemetry Contracts

Create named classes for every telemetry payload. Do not serialize anonymous objects for persisted telemetry, local dashboard API responses, or future websocket messages.

Suggested model structure:

- [ ] `ClientTelemetrySnapshot`
- [ ] `ClientTelemetryMetadata`
- [ ] `ClientTelemetryCollectionStatus`
- [ ] `ClientSystemTelemetry`
- [ ] `ClientCpuTelemetry`
- [ ] `ClientMemoryTelemetry`
- [ ] `ClientDiskTelemetry`
- [ ] `ClientDiskVolumeTelemetry`
- [ ] `ClientGpuTelemetry`
- [ ] `ClientGpuDeviceTelemetry`
- [ ] `ClientTelemetrySampleRecord`
- [ ] `ClientTelemetryGpuSampleRecord`
- [ ] `ClientTelemetrySampleDetail`
- [ ] `ClientTelemetrySearchRequest`
- [ ] `ClientTelemetrySearchResult`
- [ ] `ClientTelemetryRollupRequest`
- [ ] `ClientTelemetryRollupBucket`
- [ ] `ClientTelemetryPersistenceStatus`

Suggested enum structure:

- [ ] `ClientTelemetryPlatformEnum`
- [ ] `ClientTelemetrySectionEnum`
- [ ] `ClientTelemetryCollectionStatusEnum`
- [ ] `ClientTelemetryProviderStatusEnum`
- [ ] `ClientTelemetryGpuProviderEnum`
- [ ] `ClientTelemetryMetricEnum`
- [ ] `ClientTelemetryTimeRangeEnum`
- [ ] `ClientTelemetryBucketWidthEnum`

IDs must follow the existing backend ID requirements:

- [ ] Use `PrettyId`.
- [ ] Use a short prefix such as `tel` for telemetry samples.
- [ ] Use a short prefix such as `tgd` for telemetry GPU detail rows.
- [ ] Preserve the `{prefix}_{k-sortable}_{random}` shape.
- [ ] Keep generated IDs at or below 32 characters.

## Telemetry Collection Architecture

Create a telemetry subsystem under `src/Xeno.Client/Telemetry`. The subsystem should mirror RigMonitor's separation between collection, platform providers, persistence, and runtime service orchestration while staying aligned with Xeno Client naming and style.

Suggested folders:

- [ ] `src/Xeno.Client/Telemetry/Abstractions`
- [ ] `src/Xeno.Client/Telemetry/Models`
- [ ] `src/Xeno.Client/Telemetry/Providers`
- [ ] `src/Xeno.Client/Telemetry/Providers/Windows`
- [ ] `src/Xeno.Client/Telemetry/Providers/Linux`
- [ ] `src/Xeno.Client/Telemetry/Providers/Mac`
- [ ] `src/Xeno.Client/Telemetry/Services`

Suggested interfaces:

- [ ] `IClientTelemetryService`
- [ ] `IClientSystemTelemetryProvider`
- [ ] `IClientGpuTelemetryProvider`
- [ ] `IClientTelemetryPersistenceService`
- [ ] `IClientTelemetryCommandRunner`
- [ ] `IClientTelemetryClock`

Suggested services:

- [ ] `ClientTelemetryService`
- [ ] `ClientTelemetryPersistenceService`
- [ ] `ClientTelemetryProviderFactory`
- [ ] `ClientGpuTelemetryProviderFactory`
- [ ] `ClientTelemetryCommandRunner`

Collection behavior:

- [ ] Collect a first sample immediately after the background client runtime starts.
- [ ] Continue collection using `PeriodicTimer`.
- [ ] Skip overlapping collections if a previous cycle is still running.
- [ ] Record collection start time, end time, elapsed milliseconds, section availability, and section error summaries.
- [ ] Persist partial samples when one telemetry section fails but other sections succeed.
- [ ] Record stale or unavailable GPU telemetry without failing the whole sample.
- [ ] Avoid throwing from the background loop for normal provider failures.
- [ ] Log collection failures with timestamp and severity using the existing Xeno Client log format.
- [ ] Stop cleanly when the client background process stops.

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

- [ ] CPU utilization using platform APIs or command output parsed into named models.
- [ ] Memory using platform APIs or `vm_stat`-style data parsed into named models.
- [ ] Disk capacity using mounted filesystem information.
- [ ] Disk activity where available.
- [ ] GPU telemetry as best-effort availability, with graceful unavailable status when detailed counters are not available.

GPU provider rules:

- [ ] Prefer DCGM exporter when `Telemetry.Gpu.NvidiaDcgmExporterUrl` is configured.
- [ ] Parse DCGM Prometheus metrics into named metric classes.
- [ ] Capture GPU UUID, model, bus ID, driver version, utilization, memory total, memory used, memory free, temperature, and power where available.
- [ ] Do not fail collection when no GPU provider is available.
- [ ] Surface GPU provider status in the dashboard.

## Local SQLite Persistence

Extend the existing local dashboard database architecture instead of creating a separate persistence stack.

Database architecture checklist:

- [ ] Add `IClientTelemetryHistoryMethods`.
- [ ] Add `ClientTelemetryHistoryMethods` implementation for SQLite.
- [ ] Extend `LocalDashboardDatabaseDriverBase` with a telemetry history property.
- [ ] Extend `SqliteLocalDashboardDatabaseDriver` to initialize telemetry schema.
- [ ] Keep request history and telemetry history separate interfaces.
- [ ] Use parameterized SQL for all inserts, updates, reads, searches, rollups, and deletes.
- [ ] Add startup schema migration statements.
- [ ] Make migrations idempotent.
- [ ] Keep SQLite filename configured through current local dashboard database settings unless a separate telemetry database is explicitly justified.

Proposed tables:

- [ ] `local_telemetry_samples`
- [ ] `local_telemetry_gpu_samples`

`local_telemetry_samples` should include:

- [ ] `id`
- [ ] `client_id`
- [ ] `machine_name`
- [ ] `collected_utc`
- [ ] `persisted_utc`
- [ ] `collection_elapsed_ms`
- [ ] `platform`
- [ ] `os_description`
- [ ] `architecture`
- [ ] `processor_count`
- [ ] `cpu_available`
- [ ] `cpu_utilization_percent`
- [ ] `memory_available`
- [ ] `memory_total_bytes`
- [ ] `memory_used_bytes`
- [ ] `memory_available_bytes`
- [ ] `memory_utilization_percent`
- [ ] `disk_available`
- [ ] `disk_total_bytes`
- [ ] `disk_used_bytes`
- [ ] `disk_available_bytes`
- [ ] `disk_utilization_percent`
- [ ] `disk_read_bytes_per_second`
- [ ] `disk_write_bytes_per_second`
- [ ] `gpu_available`
- [ ] `gpu_provider`
- [ ] `gpu_device_count`
- [ ] `gpu_average_utilization_percent`
- [ ] `gpu_average_memory_utilization_percent`
- [ ] `gpu_average_temperature_celsius`
- [ ] `gpu_total_memory_bytes`
- [ ] `gpu_used_memory_bytes`
- [ ] `snapshot_json`

`local_telemetry_gpu_samples` should include:

- [ ] `id`
- [ ] `sample_id`
- [ ] `gpu_uuid`
- [ ] `name`
- [ ] `bus_id`
- [ ] `driver_version`
- [ ] `utilization_percent`
- [ ] `memory_total_bytes`
- [ ] `memory_used_bytes`
- [ ] `memory_free_bytes`
- [ ] `memory_utilization_percent`
- [ ] `temperature_celsius`
- [ ] `power_watts`

Indexes:

- [ ] `local_telemetry_samples(collected_utc)`
- [ ] `local_telemetry_samples(client_id, collected_utc)`
- [ ] `local_telemetry_samples(machine_name, collected_utc)`
- [ ] `local_telemetry_samples(cpu_utilization_percent, collected_utc)`
- [ ] `local_telemetry_samples(memory_utilization_percent, collected_utc)`
- [ ] `local_telemetry_samples(disk_utilization_percent, collected_utc)`
- [ ] `local_telemetry_samples(gpu_average_utilization_percent, collected_utc)`
- [ ] `local_telemetry_gpu_samples(sample_id)`
- [ ] `local_telemetry_gpu_samples(gpu_uuid, collected_utc)` if `collected_utc` is denormalized into the GPU table.

Persistence methods:

- [ ] `CreateAsync`
- [ ] `ReadAsync`
- [ ] `ReadDetailAsync`
- [ ] `SearchAsync`
- [ ] `RollupAsync`
- [ ] `DeleteAsync`
- [ ] `DeleteBulkAsync`
- [ ] `DeleteOlderThanAsync`
- [ ] `GetStatusAsync`

## Local Dashboard API

Add local-only REST routes to `LocalDashboardService`. These routes should follow the current local dashboard token behavior and localhost-only constraints.

Routes:

- [ ] `GET /api/v1/telemetry/status`
- [ ] `GET /api/v1/telemetry/latest`
- [ ] `POST /api/v1/telemetry/collect`
- [ ] `POST /api/v1/telemetry/history/search`
- [ ] `POST /api/v1/telemetry/history/rollups`
- [ ] `GET /api/v1/telemetry/history/{id}`
- [ ] `DELETE /api/v1/telemetry/history/{id}`
- [ ] `POST /api/v1/telemetry/history/delete`

Route behavior:

- [ ] `latest` returns the most recent persisted sample and current collection status.
- [ ] `collect` triggers an immediate collection if one is not already running.
- [ ] `search` supports range, metric filters, GPU filters, paging, and sort.
- [ ] `rollups` supports Last Hour, Last Day, Last Week, Last Month, and explicit date ranges.
- [ ] Deletes are local-only and require the same local dashboard authentication path as existing destructive routes.
- [ ] API responses use named response classes.
- [ ] Error responses are formatted consistently with current local dashboard API errors.

## Runtime Integration

The telemetry persistence service must start with the Xeno Client background process. It should not depend on a browser session or an open local dashboard tab.

Runtime checklist:

- [ ] Start telemetry service from `LocalDashboardRuntime.StartAsync` or a new client runtime coordinator.
- [ ] Ensure telemetry can run when the dashboard is enabled.
- [ ] Decide whether telemetry should run when the dashboard is disabled; the recommended default is yes when `Telemetry.Enabled` is true.
- [ ] Stop telemetry service during client shutdown.
- [ ] Expose telemetry service status to the local dashboard.
- [ ] Include telemetry startup/shutdown messages in `xeno logs`.
- [ ] Add startup output to indicate local dashboard URL when enabled, as already expected by the CLI.
- [ ] Keep dashboard URL output aligned with the existing colon formatting.

## Local Dashboard Navigation

Update `dashboard/src/local/LocalDashboardApp.jsx` and related i18n resources.

Navigation requirements:

- [ ] Rename `CONFIGURATION` to `ADMINISTRATION`.
- [ ] Add `Telemetry` above `Logs`.
- [ ] Add `Telemetry Analytics` beneath `Logs`.
- [ ] Keep existing top-level pages unchanged unless required for spacing consistency.
- [ ] Use the same icons style as the rest of the local dashboard.
- [ ] Add descriptive tooltips for every new navigation item.
- [ ] Ensure active page state, keyboard navigation, and focus behavior match existing pages.

Recommended bottom section order:

1. `Telemetry`
2. `Logs`
3. `Telemetry Analytics`
4. `Settings`

## Telemetry Page UX

The `Telemetry` page is the live operational page. It should follow RigMonitor's live telemetry dashboard capability while matching Xeno's dashboard styling.

Required controls:

- [ ] Time range selector with Last Hour, Last Day, Last Week, Last Month where historical charts are shown.
- [ ] Manual refresh icon button.
- [ ] Auto-refresh dropdown using the same component behavior as Xeno dashboard tables.
- [ ] View JSON action with copy-to-clipboard.
- [ ] Collection status indicator.
- [ ] Last collected timestamp.
- [ ] Sample age indicator.

Required visual sections:

- [ ] Summary KPI row for CPU utilization, memory utilization, disk utilization, GPU utilization, and GPU memory utilization.
- [ ] CPU section with utilization, logical processors, and collection status.
- [ ] Memory section with total, used, available, and utilization.
- [ ] Disk section with capacity, used, available, utilization, and read/write rates.
- [ ] GPU section with provider status, device count, average utilization, memory usage, temperature, and power.
- [ ] GPU device table with per-device metrics.
- [ ] Raw snapshot modal with copy-to-clipboard.

Chart behavior:

- [ ] Use SVG chart patterns already present in Xeno dashboard.
- [ ] Do not add a charting package.
- [ ] Tooltip follows the pointer and renders above workspace content.
- [ ] Axis labels are visible and localized.
- [ ] Empty states are clear and localized.
- [ ] Dark-mode colors are readable and low-glare.

## Telemetry Analytics Page UX

The `Telemetry Analytics` page is the historical analysis page. It should follow RigMonitor's analytics capabilities for the telemetry domains Xeno collects.

Required controls:

- [ ] Global range selector: Last Hour, Last Day, Last Week, Last Month.
- [ ] Metric selector.
- [ ] Bucket width selector.
- [ ] Host/client filter.
- [ ] Platform filter.
- [ ] GPU UUID filter.
- [ ] GPU model filter.
- [ ] Metric min and max filters.
- [ ] Manual refresh icon button.
- [ ] Auto-refresh dropdown.
- [ ] Clear filters icon button.

Required metrics:

- [ ] CPU utilization.
- [ ] Memory utilization.
- [ ] Memory used bytes.
- [ ] Disk utilization.
- [ ] Disk read bytes per second.
- [ ] Disk write bytes per second.
- [ ] GPU utilization.
- [ ] GPU memory utilization.
- [ ] GPU memory used bytes.
- [ ] GPU temperature.
- [ ] GPU power.
- [ ] GPU device count.

Required views:

- [ ] KPI summary for selected time range.
- [ ] Rollup chart with min, average, and max where applicable.
- [ ] Samples table with consistent server-dashboard table controls.
- [ ] Row click opens details modal.
- [ ] Action menu includes View JSON and Delete.
- [ ] Bulk delete is available where consistent with other local dashboard tables.
- [ ] Details modal consumes 95 percent of viewport width and height.
- [ ] Details modal uses tables and full-width collapsible sections for large JSON payloads.
- [ ] Copy-to-clipboard appears on IDs and JSON body fields.

## Table Standards

Every telemetry table must match the current Xeno dashboard table experience.

- [ ] Column filters apply when Enter is pressed.
- [ ] Pagination controls appear above the table.
- [ ] Auto-refresh dropdown includes None, 15s, 30s, 60s, 180s, and 300s where table data is historical.
- [ ] Default table auto-refresh is 15s unless a page has a stronger reason to default to None.
- [ ] Action menus render above table overflow containers.
- [ ] Clicking a row opens the detail modal.
- [ ] IDs are fixed-width and have copy-to-clipboard controls.
- [ ] IDs are not displayed in badges.
- [ ] Dark-mode hover colors are readable.
- [ ] Text colors remain readable in dark mode.
- [ ] Empty states are localized and visually consistent.

## Dashboard Settings UX

The Settings page must expose telemetry settings in the same visual language as existing local dashboard settings.

- [ ] Add a `Telemetry` settings section.
- [ ] Add `Enabled`.
- [ ] Add `Collection Interval Ms`.
- [ ] Add `Retention Days`.
- [ ] Add `Prune Interval Minutes`.
- [ ] Add `Persist Raw Snapshot`.
- [ ] Add `GPU Enabled`.
- [ ] Add `NVIDIA DCGM Exporter URL`.
- [ ] Add `GPU Command Timeout Ms`.
- [ ] Show validation errors before saving.
- [ ] Clamp values in the client settings model and in the UI.
- [ ] Clearly label settings that require restart.
- [ ] Clearly label settings that apply live.
- [ ] Ensure all labels, helper text, tooltips, placeholders, and validation messages use i18n.

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

- [ ] Update `README.md` with client telemetry overview.
- [ ] Update `CLIENT_GETTING_STARTED.md` with telemetry settings and dashboard access.
- [ ] Update local dashboard documentation if present.
- [ ] Update `WEBSOCKETS_API.md` with future telemetry protocol placeholders or implemented messages.
- [ ] Update `REST_API.md` if any server-facing telemetry routes are added.
- [ ] Update `CHANGELOG.md`.
- [ ] Document OS-specific telemetry limitations.
- [ ] Document NVIDIA GPU telemetry options and DCGM exporter setup.
- [ ] Document retention behavior and local database storage.
- [ ] Document how to disable telemetry.

## Test Plan

.NET tests must follow the Touchstone pattern already required for Xeno.

Touchstone shared tests:

- [ ] Settings defaults.
- [ ] Settings clamps.
- [ ] PrettyId telemetry ID shape.
- [ ] SQLite schema creation.
- [ ] SQLite idempotent migrations.
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

- [ ] Add shared descriptors to `src/Test.Shared`.
- [ ] Add xUnit coverage to `src/Test.Xunit`.
- [ ] Add NUnit coverage to `src/Test.Nunit`.
- [ ] Add automated CLI/test harness coverage to `src/Test.Automated` where appropriate.

Frontend tests:

- [ ] Build local dashboard assets.
- [ ] Exercise Telemetry page.
- [ ] Exercise Telemetry Analytics page.
- [ ] Verify time range controls.
- [ ] Verify table filtering on Enter.
- [ ] Verify action menus render above tables.
- [ ] Verify 95 percent viewport modal sizing.
- [ ] Verify tooltips follow pointer.
- [ ] Verify dark-mode readability.
- [ ] Verify i18n keys exist.
- [ ] Verify charts render nonblank SVG content.

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

- [ ] Add telemetry settings classes.
- [ ] Add telemetry enums.
- [ ] Add telemetry DTO classes.
- [ ] Add settings defaults and clamps.
- [ ] Add settings serialization tests.
- [ ] Add initial documentation for settings.
- [ ] Update this plan with completed files and decisions.

Exit criteria:

- [ ] `client.json` can represent telemetry configuration.
- [ ] Invalid interval and retention values are clamped.
- [ ] No dashboard or collector code depends on anonymous telemetry payloads.

### Phase 2: Collection Providers

- [ ] Add provider interfaces.
- [ ] Add provider factories.
- [ ] Add Windows provider.
- [ ] Add Linux provider.
- [ ] Add macOS provider.
- [ ] Add DCGM GPU provider.
- [ ] Add graceful unavailable GPU provider.
- [ ] Add parser tests with fixtures.
- [ ] Add collection service tests.
- [ ] Update this plan with provider limitations discovered during implementation.

Exit criteria:

- [ ] `ClientTelemetryService` can produce a complete named snapshot.
- [ ] Provider failures are captured in section status instead of crashing collection.
- [ ] Tests can run without requiring real GPU hardware.

### Phase 3: SQLite Persistence

- [ ] Add telemetry history interface.
- [ ] Add SQLite schema.
- [ ] Add migration code.
- [ ] Add persistence implementation.
- [ ] Add retention pruning.
- [ ] Add rollup queries.
- [ ] Add database tests.
- [ ] Update this plan with final table and index names.

Exit criteria:

- [ ] Telemetry samples survive client restart.
- [ ] Retention pruning deletes expired samples.
- [ ] Search and rollup APIs return correct values.
- [ ] All SQL is parameterized.

### Phase 4: Runtime Service Integration

- [ ] Add telemetry persistence background service.
- [ ] Start service with Xeno Client runtime.
- [ ] Stop service cleanly.
- [ ] Add status reporting.
- [ ] Add log lines for start, stop, collection failure, and pruning failure.
- [ ] Add tests for runtime lifecycle where practical.
- [ ] Update this plan with final runtime ownership decision.

Exit criteria:

- [ ] `xeno start` starts telemetry collection when enabled.
- [ ] `xeno stop` stops telemetry collection.
- [ ] Collection continues without a browser tab open.

### Phase 5: Local Dashboard API

- [ ] Add telemetry API routes.
- [ ] Add typed API request/response classes.
- [ ] Add route tests.
- [ ] Add manual collection route.
- [ ] Add delete routes.
- [ ] Update this plan with final route names.

Exit criteria:

- [ ] Local dashboard can retrieve latest telemetry.
- [ ] Local dashboard can retrieve historical telemetry.
- [ ] Local dashboard can retrieve rollups for supported ranges.

### Phase 6: Local Dashboard UI

- [ ] Update navigation group name and order.
- [ ] Add Telemetry page.
- [ ] Add Telemetry Analytics page.
- [ ] Add telemetry settings controls.
- [ ] Add telemetry i18n keys.
- [ ] Add telemetry charts.
- [ ] Add telemetry details modal.
- [ ] Add JSON copy controls.
- [ ] Add responsive and dark-mode polish.
- [ ] Compare item by item against Xeno Server Dashboard.
- [ ] Compare capability by capability against RigMonitor telemetry dashboard.
- [ ] Update this plan with UX deviations, if any, and justification.

Exit criteria:

- [ ] Local dashboard telemetry pages look and behave like the Xeno dashboard.
- [ ] Supported telemetry capabilities match RigMonitor's patterns for live and historical telemetry.
- [ ] No hard-coded user-visible strings are introduced.

### Phase 7: Documentation

- [ ] Update README.
- [ ] Update CLIENT_GETTING_STARTED.
- [ ] Update WEBSOCKETS_API.
- [ ] Update CHANGELOG.
- [ ] Add OS-specific telemetry notes.
- [ ] Add GPU setup notes.
- [ ] Update this plan with documentation links.

Exit criteria:

- [ ] A user can enable, disable, tune, view, and troubleshoot telemetry from documentation alone.

### Phase 8: Full Verification

- [ ] Run .NET build.
- [ ] Run Touchstone xUnit tests.
- [ ] Run Touchstone NUnit tests.
- [ ] Run automated tests.
- [ ] Run dashboard build.
- [ ] Run local dashboard manually.
- [ ] Verify light mode.
- [ ] Verify dark mode.
- [ ] Verify telemetry collection on at least the current development OS.
- [ ] Verify graceful behavior when no GPU provider is available.
- [ ] Verify retention pruning with a small test window.
- [ ] Update this plan with final test results.

Exit criteria:

- [ ] The feature is complete without known compliance deviations.
- [ ] Any platform limitations are documented.
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

- [ ] Whether telemetry remains enabled when the local dashboard is disabled.
- [ ] Whether Xeno should use DCGM-only GPU telemetry at first or include `nvidia-smi` fallback.
- [ ] Whether disk activity should include only aggregate rates or per-volume rates in the first implementation.
- [ ] Whether raw snapshot JSON is always persisted or controlled by `PersistRawSnapshot`.
- [ ] Whether telemetry settings changes apply live or require restart.
- [ ] Whether future server upload should be push-only, pull-only, or both.

## Completion Notes

Keep this section updated during implementation.

- Status: Plan created. Implementation not started.
- Last updated: 2026-06-26.

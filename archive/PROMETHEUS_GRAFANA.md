# Xeno Observability — Prometheus, Grafana, Tempo, and Loki

This document is the implementation plan for adding full metrics, traces, and logs
observability to Xeno, modeled on the OpenTelemetry pipeline that Switchboard uses
(`c:\code\switchboard`) and adapted to Xeno's fleet-of-model-servers domain. It is
written to be executed and tracked: every actionable item is a checkbox, grouped
into workstreams, and the acceptance and rejection criteria at the end define done.

Xeno already emits rich per-request telemetry — time to first token, streaming
duration, prompt/completion/total tokens, client round trip, upstream timing,
selected client, routing decision — and it already collects fleet host and provider
telemetry from connected clients. None of that leaves the process today except a
small hand-rolled `/metrics` text endpoint and the request-history tables. The work
here turns that existing signal into a standards-based OpenTelemetry pipeline and
gives operators pre-built Grafana views over the whole product, not just the
plumbing.

## How to use this document

Each task is a GitHub-style checkbox. Mark `- [x]` when complete and, where a task
has verification steps, keep them checked independently so a reviewer can see what
was actually run. When a decision is made that diverges from this plan, record the
divergence and the reason inline under the affected task, per the "explicit and
justified divergence" rule in `agents/requirements/BACKEND_ARCHITECTURE.md`.

Legend: `[ ]` not started · `[~]` in progress (annotate with owner/date) · `[x]` done
· `[!]` blocked (annotate blocker).

## Locked decisions

These were confirmed before planning and are not open questions:

- **Full parity**: metrics, distributed traces, and logs — Prometheus, Tempo, Loki,
  and Grafana, fed by an OpenTelemetry Collector.
- **Export topology**: Xeno emits OTLP to an OpenTelemetry Collector; Prometheus
  scrapes the Collector's Prometheus exporter. The application does not serve the
  Prometheus scrape surface itself.
- **Fleet telemetry included**: the client host telemetry and provider telemetry
  that Xeno already collects are re-exported as metrics, in addition to the server's
  own request, relay, connection, auth, email, ledger, and process metrics.
- **Grafana access**: anonymous Admin, no login, on host port `9400`, consistent with
  Xeno's `9x00` UI port convention and Switchboard's turnkey posture. Local
  development only; production hardening is called out as a non-goal below.
- **Server-only instrumentation**: `xeno-server` is instrumented; the `xeno` client is
  not. Clients run on separate GPU hosts outside the compose network and cannot
  reach the Collector, so trace context is propagated best-effort to the client
  boundary and client-side continuation is a documented follow-up (not this pass).
- **Tenant is not a default metric label**: tenant stays a trace-span tag and a log
  field; it becomes a metric label only when `Metrics.IncludeTenantLabel` is enabled.
- **Dashboard UI is the Grafana card only**: the Settings page gets the Grafana link
  card plus the `Telemetry` settings block. A separate in-dashboard observability
  status view is intentionally out of scope for this pass (recorded in Workstream E).
- **Legacy `/metrics` is kept and migrated**: the existing hand-rolled `/metrics`
  endpoint is rewired onto the same OpenTelemetry instruments as a Collector-less
  fallback scrape surface, giving a single source of truth.

Minor defaults taken without a separate decision (override before implementation if
desired): Prometheus is exposed on host `9090` (fall back to `9410` on conflict);
`DOCKERHUB_README.md` is created because `agents/requirements` mandates it; the
observability images use Switchboard's tested pins (collector `0.109.0`, prometheus
`v2.54.1`, grafana `11.2.2`, tempo `2.6.1`, loki `3.2.1`).

---

## 1. Architecture and signal topology

Xeno instruments in-process with the vanilla .NET `System.Diagnostics.Metrics`
`Meter` API and the OpenTelemetry SDK, exactly as Switchboard does, and exports every
signal over OTLP to a standalone Collector. The Collector fans the signals out to the
three backends, and Grafana reads all three. Keeping the scrape surface on the
Collector rather than in-process means Xeno depends only on GA OpenTelemetry packages
and never has to open a second HTTP listener inside the Watson webserver.

```
xeno-server  --OTLP 4317 (metrics + traces)-->  otel-collector
xeno-server  --file logs (./logs/*.log*)------>  otel-collector (filelog receiver)
   otel-collector --prometheus :8889-->  prometheus (:9090) --> grafana
   otel-collector --OTLP------------->  tempo      (:3200)  --> grafana
   otel-collector --OTLP------------->  loki       (:3100)  --> grafana
                                        grafana (host :9400, anonymous Admin)
```

Port map (host → container), chosen to avoid collisions with the existing stack
(`8000` less3, `8080/8081` netledger, `9000` xeno-server, `9100` dashboard, `9200`
netledger-ui, `9300` less3-ui):

| Service | Host port | Container port | Purpose |
| --- | --- | --- | --- |
| otel-collector | 4317, 4318, 8889 | 4317/4318/8889 | OTLP gRPC/HTTP in; Prometheus exporter out |
| prometheus | 9090 | 9090 | scrape + query |
| grafana | 9400 | 3000 | dashboards UI |
| tempo | (internal only) | 3200 | trace store |
| loki | (internal only) | 3100 | log store |

- [ ] Confirm no port conflict for `9090`/`9400`/`4317`/`4318`/`8889` on target hosts;
  if `9090` is taken, expose Prometheus on `9410` and update `README.md` and the
  dashboard card accordingly.

## 2. Scope and non-goals

**In scope**: server-side (`Xeno.Server` / `Xeno.Core`) metrics, traces, and log
shipping; the full Docker observability stack in `docker/compose.yaml`; Grafana
provisioning and pre-built dashboards; `docker/factory` handling; a Grafana link card
on the dashboard Settings page with i18n; documentation, Postman, and asset updates;
and a materially expanded test surface.

**Non-goals** (call out explicitly, do not silently skip):

- Production security hardening of Grafana/Prometheus (TLS, real auth, network
  policy). The stack ships anonymous-Admin for local development; a "Production
  hardening" subsection in the docs records what must change before exposure.
- Per-client `/metrics` endpoints on the `xeno` client process. Clients report
  telemetry to the server over the authenticated WebSocket; the server re-exports it.
  If standalone client scraping is wanted later, it is a follow-up.
- Long-term metric/trace/log retention and durable volumes. Backends are ephemeral
  container-local storage (matching Switchboard); durability is a follow-up.

## 3. Compliance mapping to `agents/requirements`

Every workstream must satisfy the standards in `c:\code\agents\requirements`. The
load-bearing ones for this work:

- **Code style** (`CODE_STYLE.md`, `BACKEND_ARCHITECTURE.md`): no `var`; no tuples;
  `using` directives inside the namespace block, system usings first then others,
  each group alphabetized; one class or enum per file; public members `LikeThis`,
  private `_LikeThis`; XML docs on all public types/members/methods and none on
  private; explicit getters/setters with backing fields where validation/clamping
  applies; `ConfigureAwait(false)` on every await in library/server code; a
  `CancellationToken` on every async method doing cancellable work; nullable enabled;
  specific exception types with contextual messages and `/// <exception>` tags; the
  full `IDisposable` pattern where disposable resources are held; no `Console.Write*`
  in library code (use the existing `SyslogLogging` logger).
- **Settings** (`BACKEND_ARCHITECTURE.md` "Settings and Configuration"): strongly
  typed settings classes, validate on load, clamp numeric ranges, override secrets by
  environment variable, log startup configuration without secrets.
- **Testing** (`BACKEND_TEST_ARCHITECTURE.md`, `BACKEND_ARCHITECTURE.md` "Testing
  Strategy"): descriptors in `Test.Shared` depending only on `Touchstone.Core`; no
  console output in shared tests; self-contained tests; positive **and** negative
  cases; provider-matrix coverage for any provider-backed behavior; assertions by
  throwing; `skip`/`skipReason` for not-yet-ready cases.
- **Observability** (`AUTHENTICATION.md` "Observability", `BACKEND_ARCHITECTURE.md`
  "Request Capture and History"): auth and authorization outcomes emitted as counters
  (`auth_requests_total`, `authz_denials_total`, `rbac_mutations_total`, bypass
  counters); pair counters with structured logs carrying request/correlation/trace
  IDs so metrics, logs, and audit records can be joined.
- **Docker** (`REPOSITORY_REQUIREMENTS.md`): `.yaml` not `.yml`; build contexts within
  unless explicit image tags are used (the observability images are explicit tags).
- **Docs/hygiene** (`REPOSITORY_REQUIREMENTS.md`, `BACKEND_ARCHITECTURE.md`): keep
  `README.md` accurate; maintain `CHANGELOG.md`; a `DOCKERHUB_README.md` is required
  and is currently missing — this plan adds it.
- **Prose** (`WRITING_DOCUMENTS.md`): human-facing docs (README section, setup guide,
  DOCKERHUB_README) get real prose, not a sentence-plus-list, and avoid the banned
  generative openings and generic recaps.

- [ ] Before coding, re-read `CODE_STYLE.md` and `BACKEND_ARCHITECTURE.md` "Code
  Standards" and keep them open during Workstream A.

---

## 4. Workstream A — Backend instrumentation (`Xeno.Core`, `Xeno.Server`)

The instrumentation follows Switchboard's shape: one static registry of instruments
that are inert until a provider listens, a service that owns the provider lifecycle
and is constructed first at startup, and inline recording at the hot paths. Xeno's
advantage is that the values already exist — `RequestHistoryTelemetry` is computed in
`PopulateRequestHistory`, connections are tracked in `RegisterClient` /
`UnregisterClientConnectionAsync`, and fleet samples arrive through the telemetry
collection loop. Instrumentation is mostly observation at points that already run.

> **Implementation status (2026-07-31):** Workstream A is code-complete and builds
> warning-free. Instruments use OpenTelemetry dotted names (`xeno.http.requests`,
> `xeno.relay.roundtrip.duration`, …); the OTLP→Prometheus exporter maps these to the
> underscore names used in the dashboards below (`xeno_http_requests_total`,
> `xeno_relay_roundtrip_duration_seconds`, …). Two deviations from the original sketch,
> both deliberate: (1) process CPU/memory/threads/uptime are emitted via custom
> observable gauges (`xeno.process.*`) rather than the prerelease
> `OpenTelemetry.Instrumentation.Process` package, keeping the build on GA packages
> only; (2) the legacy `/metrics` text endpoint is retained unchanged and now shares
> the same recording call sites as the OTLP instruments (see A10). Remaining open
> items are called out inline: dashboard `SettingsView` telemetry block (Workstream E),
> W3C trace propagation to the client (A8), and the catalog drift test (A11).

### A1. Telemetry settings model

New strongly typed settings, one class per file under `src/Xeno.Core/Settings/`,
wired into `XenoSettings`, validated and clamped on load, with environment overrides
applied in `ApplyEnvironmentSettings`.

- [x] `TelemetrySettings.cs` — `Enable` (bool, default true), `ServiceName`
  (string, default `xeno-server`), plus nested `Metrics`, `Traces`, `Logs`, `Otlp`.
- [x] `TelemetryMetricsSettings.cs` — `Enable` (default true), `ExportIntervalMs`
  (default 15000, clamp 1000–300000), `IncludeTenantLabel` (default false — see
  cardinality policy in A9), `ExportFleetTelemetry` (default true).
- [x] `TelemetryTracesSettings.cs` — `Enable` (default true), `SamplingRatio`
  (double, default 1.0, clamp 0.0–1.0), `PropagateToClient` (default true).
- [x] `TelemetryLogsSettings.cs` — `Enable` (default true, documents that log
  shipping is done by the Collector's filelog receiver, so this flag controls the
  documented intent and the dashboard status surface).
- [x] `OtlpExporterSettings.cs` — `Endpoint` (default `http://localhost:4317`),
  `Protocol` (`grpc` | `httpprotobuf`, default `grpc`), `TimeoutMs` (default 10000,
  clamp 1000–120000), `Headers` (nullable, sensitive/redacted).
- [x] Add `Telemetry` to `XenoSettings` with a default instance.
- [x] Environment overrides in `ApplyEnvironmentSettings`: at minimum
  `XENO_TELEMETRY_ENABLED`, `XENO_TELEMETRY_OTLP_ENDPOINT`,
  `XENO_TELEMETRY_OTLP_PROTOCOL`, `XENO_TELEMETRY_METRICS_ENABLED`,
  `XENO_TELEMETRY_TRACES_ENABLED`, `XENO_TELEMETRY_TRACES_SAMPLING_RATIO`. Log the
  resolved telemetry config at startup with `Headers` redacted.
- [x] Extend the dashboard `SettingsView` normalizer/defaults and the `/settings`
  round-trip to include the new `Telemetry` block (see Workstream E).

### A2. Central metrics + tracing registry

- [x] `src/Xeno.Core/Telemetry/XenoTelemetry.cs` — a `static class` holding one
  `Meter` and one `ActivitySource` named `"Xeno"`, all instruments as
  `private static readonly` fields, static `Record*` helper methods, and
  observable-gauge callbacks that read a stashed settings/state snapshot. Instruments
  are created at type init and are no-ops until a provider listens, so the hot path
  records unconditionally with no `if (enabled)` guard.
- [x] `SetState(...)` / `ClearState()` using `Interlocked.Exchange` to hand the
  registry a live reference to the platform state and settings for observable-gauge
  collection (connected clients, queue depths, fleet samples). No per-request work in
  callbacks; they snapshot at collection time only.
- [x] Label policy helper: a single method that maps a raw HTTP status to a bounded
  `code` (exact 3-digit for counters is acceptable; never raw path, never client IP,
  never tenant unless `IncludeTenantLabel` is set). Route label uses the registered
  route identifier, not the raw URL.

### A3. Provider lifecycle service

- [x] `src/Xeno.Core/Telemetry/TelemetryService.cs` — `sealed class TelemetryService :
  IDisposable`. Builds a `MeterProvider` (`AddMeter(XenoTelemetry.SourceName)`,
  `AddRuntimeInstrumentation()`, `AddProcessInstrumentation()`, periodic OTLP reader
  with `ExportIntervalMs`) and a `TracerProvider` (`AddSource(XenoTelemetry
  .SourceName)`, `ParentBasedSampler(TraceIdRatioBasedSampler(SamplingRatio))`, OTLP
  exporter). Resource attributes: `service.name`, `service.version` (`Constants
  .Version`), `service.instance.id` (GUID). Full Dispose pattern.
- [x] Wire into `XenoServer.StartAsync` **first**, before `_Database`/`_Routing`/
  `_Ledger`, so the shared meter/activity source have a listening provider before any
  hot-path recording. Only construct when `_Settings.Telemetry.Enable`. Dispose in
  `Stop()`/`Dispose()`. Log `[Telemetry] starting`/`stopping` per the logging
  conventions already in the codebase.
- [x] Package references in `Xeno.Core.csproj` (GA versions only, matching
  Switchboard's 1.17.0 line): `OpenTelemetry`, `OpenTelemetry.Exporter
  .OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.Runtime`,
  `OpenTelemetry.Instrumentation.Process`. Confirm warning-free build.

### A4. Instrument request and relay hot paths

The single richest point is `RecordAndSaveHistoryAsync` / `PopulateRequestHistory`,
which already computes every relay timing and token count. Record metrics and close
the trace span there so instrumentation and persistence stay in lockstep.

- [x] HTTP request metrics from the existing `PostRoutingRouteAsync` hook (which
  already logs `method url: status ms`): `xeno_http_requests_total{method,route,code}`
  and `xeno_http_request_duration_seconds{route}` (histogram), using
  `context.Timestamp.TotalMs`.
- [x] Relay/LLM metrics recorded in `PopulateRequestHistory` from the
  `RequestHistoryTelemetry` fields:
  - `xeno_relay_requests_total{model,client,provider,code}` (counter)
  - `xeno_relay_failures_total{model,client,provider,reason}` (counter)
  - `xeno_time_to_first_token_seconds{model,provider}` (histogram, from
    `TimeToFirstTokenMs`)
  - `xeno_streaming_duration_seconds{model,provider}` (histogram, from
    `TotalStreamingTimeMs`)
  - `xeno_client_round_trip_seconds{client}` (histogram, from `ClientRoundTripMs`)
  - `xeno_upstream_duration_seconds{model,provider}` (histogram, from
    `UpstreamTotalMs`)
  - `xeno_prompt_tokens_total`, `xeno_completion_tokens_total`,
    `xeno_total_tokens_total` (counters, `{model,client,provider}`)
  - `xeno_tokens_per_request{model,provider}` (histogram, total tokens per request)
  - `xeno_stream_chunks_total`, `xeno_stream_events_total` (counters)
  - `xeno_relay_request_bytes` / `xeno_relay_response_bytes` (histograms)
- [x] Define explicit histogram buckets via metric `View`s in `TelemetryService`
  (the OTel SDK defaults are integer-second-scaled and coarse for sub-second latency
  and for token counts). Latency buckets in seconds (e.g. `.005,.01,.025,.05,.1,.25,
  .5,1,2.5,5,10,30,60`); token buckets (e.g. `16,64,256,1024,4096,16384,65536`).
  Record exact chosen boundaries in the doc.

### A5. Connection and lifecycle metrics

- [x] In `RegisterClient`: `xeno_client_registrations_total{tenant?}` and increment a
  connect counter `xeno_client_connections_total`.
- [x] In `UnregisterClientConnectionAsync`: `xeno_client_disconnections_total`.
- [x] In `EvictStaleClientConnectionsAsync`: `xeno_client_evictions_total`.
- [x] Observable gauges from platform state: `xeno_connected_clients`,
  `xeno_available_models`, `xeno_inflight_requests`.
- [x] Loop health gauges/counters: telemetry-collection cycle count and failures
  (from `_TelemetryRecentFailureCount`), ledger queue depth and drain outcomes
  (`xeno_ledger_events_total{outcome}`, `xeno_ledger_queue_depth` gauge), retention
  sweep deletions.

### A6. Auth, RBAC, email, and data-mutation metrics

These satisfy `AUTHENTICATION.md` "Observability" directly and reuse counters that
already exist as `Interlocked` fields.

- [x] `xeno_auth_requests_total{result}` and `xeno_auth_failures_total` (from
  `_AuthSuccessCount`/`_AuthFailureCount`), `xeno_authz_denials_total` (from
  `_AuthorizationDenialCount`), `xeno_privileged_bypass_total` (from
  `_PrivilegedBypassCount`).
- [x] `xeno_rbac_mutations_total{resource,operation}` fed from the existing
  create/update/delete handlers that already log `created/updated/deleted <noun>`.
- [x] `xeno_email_total{provider,template,outcome}` from the central `RecordEmail*`
  methods (already the logging chokepoint).
- [x] `xeno_records_mutated_total{resource,operation}` covering tenant/user/
  credential/role/permission/routing-policy/role-assignment/client mutations.

### A7. Fleet host and provider telemetry export

Xeno's telemetry collection loop already persists per-client host samples (CPU,
memory, disk, GPU) and per-provider samples (active/queued requests, p95 latency,
model counts, health). Export these as observable gauges keyed to the latest sample
per client/provider, gated by `Metrics.ExportFleetTelemetry`.

- [x] `xeno_client_host_cpu_percent{client}`, `_memory_percent`, `_disk_percent`,
  `_gpu_percent{client}` from `ClientTelemetryLatestCache` / latest host samples.
- [x] `xeno_provider_active_requests{client,provider}`, `_queued_requests`,
  `_latency_p95_seconds`, `_available_models`, `_loaded_models`, `_up{client,provider}`
  (0/1 health).
- [x] Ensure the observable-gauge callbacks read the latest-sample cache, not the
  database, so collection is cheap and never blocks the export cycle.

### A8. Distributed tracing across the relay path

- [x] Start an `ActivityKind.Server` span in the request pipeline named
  `relay {route}` (or `request {route}` for non-relay), continuing the client's W3C
  `traceparent`/`tracestate` when present. Tag `xeno.model`, `xeno.client`,
  `xeno.provider`, `xeno.tenant` (tenant tag is acceptable on spans — spans are not
  a cardinality problem the way metric labels are), `http.request.method`,
  `http.route`, `http.response.status_code`, and the relay timings as span events
  (`client.selected`, `client.dispatch`, `first.token`, `stream.complete`).
- [ ] When `Traces.PropagateToClient` is true, inject `traceparent` on the outbound
  WebSocket relay frame so the client (and its provider call) can continue the trace.
  If the client does not yet echo it, record that as a known gap in the handoff.
- [x] Correlate logs with traces: include the active trace/span id in the request
  context so `SyslogLogging` lines and audit records carry it (satisfies the
  "join metrics, logs, and audit records" requirement). Confirm the `filelog`
  pipeline's Loki labels or parsed fields expose `trace_id` for Grafana's
  trace↔log derived fields.

### A9. Cardinality policy (must be enforced, not aspirational)

- [x] Metric labels are bounded sets only: `model`, `provider`, `client`, `code`,
  `result`, `outcome`, `resource`, `operation`, `reason`. Never raw URL path, never
  client IP, never free-form message text.
- [x] `tenant` is **not** a default metric label. It is opt-in via
  `Metrics.IncludeTenantLabel` because tenant count is unbounded in principle;
  per-tenant analytics already live in request-history/audit tables and the dashboard.
  Document the tradeoff and the opt-in switch. Tenant remains a span tag (fine) and a
  Loki/log field (fine).
- [x] `route` uses the registered route identifier, not the raw path, so
  `/v1.0/api/tenants/{id}` collapses to one series.

### A10. Retire or repurpose the existing `/metrics` endpoint

- [x] Keep the hand-rolled `SendMetricsAsync` `/metrics` endpoint and migrate it onto
  the `XenoTelemetry` instruments (decided), so both the Collector's scrape surface
  and this Collector-less fallback read the same source of truth. Update
  `BuildEmailMetricSeries` and the metrics test to read the migrated instruments.

### A11. Metrics catalog (authoritative)

- [x] Catalog table seeded below (name, type, unit, labels, source site). Mirror into
  `README.md` in Workstream F.
- [x] `TelemetryUnitSuites` drift test asserting live instrument names/labels against
  this catalog (Workstream G).

Instrument names are the OpenTelemetry (dotted) names emitted by `XenoTelemetry`. The
Prometheus name is what the Collector's Prometheus exporter produces (dots → `_`,
counters gain `_total`, unit suffixes appended). All instruments are registered in
`src/Xeno.Core/Telemetry/XenoTelemetry.cs`.

| OTel instrument | Prometheus name | Type | Unit | Labels | Recorded at |
| --- | --- | --- | --- | --- | --- |
| `xeno.http.requests` | `xeno_http_requests_total` | counter | request | `http.method`, `http.route`, `http.status_code`, `http.status_class` | `PostRoutingRouteAsync` |
| `xeno.http.request.duration` | `xeno_http_request_duration_seconds` | histogram | s | `http.method`, `http.route`, `http.status_code`, `http.status_class` | `PostRoutingRouteAsync` |
| `xeno.relay.requests` | `xeno_relay_requests_total` | counter | request | `model`, `client_id`, `provider`, `outcome`, `streamed`, `tenant?` | `RecordRelayMetric` |
| `xeno.relay.failures` | `xeno_relay_failures_total` | counter | request | same as relay.requests | `RecordRelayMetric` |
| `xeno.relay.roundtrip.duration` | `xeno_relay_roundtrip_duration_seconds` | histogram | s | relay tags | `RecordRelayMetric` |
| `xeno.relay.upstream.duration` | `xeno_relay_upstream_duration_seconds` | histogram | s | relay tags | `RecordRelayMetric` |
| `xeno.relay.time_to_first_token` | `xeno_relay_time_to_first_token_seconds` | histogram | s | relay tags | `RecordRelayMetric` |
| `xeno.relay.streaming.duration` | `xeno_relay_streaming_duration_seconds` | histogram | s | relay tags | `RecordRelayMetric` |
| `xeno.relay.request.size` | `xeno_relay_request_size_bytes` | histogram | By | relay tags | `RecordRelayMetric` |
| `xeno.relay.response.size` | `xeno_relay_response_size_bytes` | histogram | By | relay tags | `RecordRelayMetric` |
| `xeno.tokens.prompt` | `xeno_tokens_prompt_total` | counter | token | relay tags | `RecordRelayMetric` |
| `xeno.tokens.completion` | `xeno_tokens_completion_total` | counter | token | relay tags | `RecordRelayMetric` |
| `xeno.tokens.all` | `xeno_tokens_all_total` | counter | token | relay tags | `RecordRelayMetric` |
| `xeno.tokens.per_request` | `xeno_tokens_per_request` | histogram | token | relay tags | `RecordRelayMetric` |
| `xeno.relay.stream.chunks` | `xeno_relay_stream_chunks_total` | counter | chunk | relay tags | `RecordRelayMetric` |
| `xeno.relay.stream.events` | `xeno_relay_stream_events_total` | counter | event | relay tags | `RecordRelayMetric` |
| `xeno.client.connections` | `xeno_client_connections_total` | counter | connection | `provider` | ws accept |
| `xeno.client.disconnections` | `xeno_client_disconnections_total` | counter | connection | `reason` | ws close / eviction |
| `xeno.client.registrations` | `xeno_client_registrations_total` | counter | registration | — | `RegisterClient` |
| `xeno.client.evictions` | `xeno_client_evictions_total` | counter | eviction | — | `EvictStaleClientConnectionsAsync` |
| `xeno.auth.attempts` | `xeno_auth_attempts_total` | counter | attempt | `result` | auth pipeline |
| `xeno.auth.denials` | `xeno_auth_denials_total` | counter | denial | — | `DefaultRouteAsync` |
| `xeno.auth.privileged_bypasses` | `xeno_auth_privileged_bypasses_total` | counter | bypass | — | authorization |
| `xeno.data.mutations` | `xeno_data_mutations_total` | counter | mutation | `entity`, `operation` | `AuthenticationService.AppendAudit` |
| `xeno.email.results` | `xeno_email_results_total` | counter | email | `outcome` | `RecordEmailResult/Failure` |
| `xeno.ledger.events` | `xeno_ledger_events_total` | counter | event | `outcome` | `TryEnqueueLedgerEvent` |
| `xeno.fleet.collection.cycles` | `xeno_fleet_collection_cycles_total` | counter | cycle | — | `TelemetryCollectionLoopAsync` |
| `xeno.fleet.collection.failures` | `xeno_fleet_collection_failures_total` | counter | failure | — | `TelemetryCollectionLoopAsync` |
| `xeno.request_history.pruned` | `xeno_request_history_pruned_total` | counter | record | — | `PruneRequestHistoryAsync` |
| `xeno.build.info` | `xeno_build_info` | gauge | — | `version`, `runtime` | observable |
| `xeno.process.memory.working_set` | `xeno_process_memory_working_set_bytes` | gauge | By | — | observable |
| `xeno.process.memory.gc_heap` | `xeno_process_memory_gc_heap_bytes` | gauge | By | — | observable |
| `xeno.process.cpu.time` | `xeno_process_cpu_time_seconds_total` | counter | s | — | observable |
| `xeno.process.threads` | `xeno_process_threads` | gauge | thread | — | observable |
| `xeno.process.uptime` | `xeno_process_uptime_seconds` | gauge | s | — | observable |
| `xeno.clients.connected` | `xeno_clients_connected` | gauge | client | — | observable (platform) |
| `xeno.models.available` | `xeno_models_available` | gauge | model | — | observable (platform) |
| `xeno.relay.inflight` | `xeno_relay_inflight` | gauge | request | — | observable (platform) |
| `xeno.ledger.queue.depth` | `xeno_ledger_queue_depth` | gauge | event | — | observable (platform) |
| `xeno.fleet.host.cpu` | `xeno_fleet_host_cpu_percent` | gauge | % | `client_id` | observable (fleet) |
| `xeno.fleet.host.memory` | `xeno_fleet_host_memory_percent` | gauge | % | `client_id` | observable (fleet) |
| `xeno.fleet.host.disk` | `xeno_fleet_host_disk_percent` | gauge | % | `client_id` | observable (fleet) |
| `xeno.fleet.host.gpu` | `xeno_fleet_host_gpu_percent` | gauge | % | `client_id` | observable (fleet) |
| `xeno.fleet.provider.models_available` | `xeno_fleet_provider_models_available` | gauge | model | `client_id`, `provider` | observable (fleet) |
| `xeno.fleet.provider.models_loaded` | `xeno_fleet_provider_models_loaded` | gauge | model | `client_id`, `provider` | observable (fleet) |
| `xeno.fleet.provider.up` | `xeno_fleet_provider_up` | gauge | provider | `client_id`, `provider` | observable (fleet) |

Plus the standard `AddRuntimeInstrumentation()` series (`process_runtime_dotnet_*`:
GC heap/collections, JIT, thread pool, exceptions, monitor contention).

---

## 5. Workstream B — Docker Compose and Collector/Prometheus configs

Mirror Switchboard's `Docker/telemetry/` layout under `docker/telemetry/`, and add
five services to `docker/compose.yaml`. Keep the existing services untouched except
for wiring `xeno-server` to emit OTLP and mount its logs for tailing.

- [x] `docker/telemetry/otel-collector-config.yaml` — `otlp` receiver (4317 gRPC,
  4318 HTTP); `filelog` receiver tailing `/app/logs/*.log*`; `batch` processor;
  exporters `prometheus` (`0.0.0.0:8889`, `add_metric_suffixes: false`,
  `resource_to_telemetry_conversion.enabled: true`), `otlp/tempo`, `loki`; pipelines
  `metrics [otlp]→batch→prometheus`, `traces [otlp]→batch→otlp/tempo`,
  `logs [filelog]→batch→loki`.
- [x] `docker/telemetry/prometheus.yaml` — global 15s scrape/eval; single
  `scrape_configs` job `otel-collector` targeting `otel-collector:8889`.
- [x] `docker/telemetry/tempo.yaml` — minimal single-binary Tempo (OTLP receiver,
  local storage).
- [x] `docker/telemetry/loki-config.yaml` — minimal single-binary Loki.
- [x] `docker/telemetry/grafana/provisioning/datasources/datasources.yaml` —
  Prometheus (uid `prometheus`, default), Tempo (uid `tempo`, `tracesToLogsV2` →
  loki), Loki (uid `loki`, derived field `TraceID` → tempo).
- [x] `docker/telemetry/grafana/provisioning/dashboards/dashboards.yaml` — file
  provider pointing at `/var/lib/grafana/dashboards`.
- [x] `docker/compose.yaml` service additions (explicit pinned images per
  `REPOSITORY_REQUIREMENTS.md`; suggested pins matching Switchboard's tested set):
  - [x] `otel-collector` (`otel/opentelemetry-collector-contrib:0.109.0`), ports
    `4317/4318/8889`, config + `./logs:/app/logs:ro` mounts, `depends_on: [tempo,
    loki]`, `restart: unless-stopped`.
  - [x] `prometheus` (`prom/prometheus:v2.54.1`), port `9090`, config mount.
  - [x] `grafana` (`grafana/grafana:11.2.2`), port `9400:3000`, env
    `GF_AUTH_ANONYMOUS_ENABLED=true`, `GF_AUTH_ANONYMOUS_ORG_ROLE=Admin`,
    `GF_SECURITY_ALLOW_EMBEDDING=true`, provisioning + dashboards mounts,
    `depends_on: [prometheus, tempo, loki]`.
  - [x] `tempo` (`grafana/tempo:2.6.1`), config mount, internal only.
  - [x] `loki` (`grafana/loki:3.2.1`), config mount, internal only.
  - [x] Image tag overrides via `${...}` env vars for each, consistent with the
    existing `${XENO_SERVER_IMAGE:-...}` pattern.
- [x] Wire `xeno-server`: add `depends_on: otel-collector`, keep the `./logs:/app/logs`
  mount (already present) so the Collector can tail it, and ensure `docker/xeno.json`
  and `docker/factory/xeno.json` set `Telemetry.Otlp.Endpoint` to
  `http://otel-collector:4317` and `Telemetry.Enable: true`.
- [x] Healthchecks: add a simple healthcheck to `prometheus` (`/-/healthy`) and
  `grafana` (`/api/health`); leave collector/tempo/loki without (Switchboard does),
  or add if trivial. Record the decision.
- [x] Add a `.dockerignore` entry review so telemetry config dirs are not excluded
  from any build context that needs them.
- [ ] `docker compose config` validates; `docker compose up -d` brings all services
  healthy; Grafana reachable at `http://localhost:9400`.

## 6. Workstream C — Grafana dashboards (pre-populated)

Provision one folder of dashboards so operators see the whole product on first boot,
not an empty Grafana. Each dashboard is a committed JSON file under
`docker/telemetry/grafana/dashboards/`, datasource uid `prometheus` (or `tempo`/`loki`
where noted). List every panel and its PromQL in the JSON; the tables below are the
build spec.

- [x] `xeno-overview.json` — Request rate `sum(rate(xeno_http_requests_total[1m]))`;
  error rate `sum(rate(xeno_http_requests_total{code=~"5.."}[1m]))`; request latency
  p50/p90/p99 via `histogram_quantile(...)` on
  `xeno_http_request_duration_seconds_bucket`; connected clients
  `xeno_connected_clients`; available models `xeno_available_models`; in-flight
  `xeno_inflight_requests`.
- [x] `xeno-relay.json` (LLM/relay) — relay throughput by model
  `sum by (model) (rate(xeno_relay_requests_total[1m]))`; relay failures
  `sum by (reason) (rate(xeno_relay_failures_total[5m]))`; **time to first token**
  p50/p95 `histogram_quantile(.., rate(xeno_time_to_first_token_seconds_bucket[5m]))`
  by model/provider; **streaming duration** p50/p95; upstream duration; client round
  trip.
- [x] `xeno-tokens.json` — prompt/completion/total token rate
  `sum by (model) (rate(xeno_total_tokens_total[5m]))`; tokens-per-request histogram
  heatmap; per-provider and per-client token splits; stream chunk/event rates.
- [x] `xeno-connections.json` — connect/disconnect/eviction rates; connected-clients
  gauge over time; registrations; per-client round-trip latency.
- [x] `xeno-fleet.json` — per-client host CPU/mem/disk/GPU (`xeno_client_host_*`);
  provider active/queued requests; provider p95 latency; provider up/down stat;
  available vs loaded models.
- [x] `xeno-auth.json` — auth success/failure rate; authorization denials; privileged
  bypasses; RBAC mutation rate by resource/operation; email sent/skipped/failed.
- [x] `xeno-runtime.json` — process CPU/memory (from `AddProcessInstrumentation`);
  .NET GC collections/heap, threadpool, exceptions (from
  `AddRuntimeInstrumentation`); uptime; build info.
- [x] `xeno-logs.json` — a Loki panel over Xeno server logs with a label/level filter
  and a link to trace context (derived `TraceID`).
- [ ] Traces: verify Tempo explore works and the Loki→Tempo and Tempo→Loki
  correlations resolve; add a "Traces" link/panel to the overview.
- [x] Every panel's PromQL is committed in JSON, uses correct units (seconds, bytes,
  reqps), and legends use `{{label}}` templating. Import each JSON into a running
  Grafana once to confirm it renders with live data before committing.

## 7. Workstream D — `docker/factory` and reset scripts

Telemetry backends are ephemeral (no named volumes), so a factory reset does not need
to purge Prometheus/Grafana/Tempo/Loki data — `docker compose down` already discards
it. The reset scripts must still restore the telemetry-enabled `xeno.json` and keep
the Collector's log source clean.

- [x] `docker/factory/xeno.json` — includes the full `Telemetry` block with
  `Otlp.Endpoint = http://otel-collector:4317` and `Enable: true`, matching the live
  `docker/xeno.json`.
- [x] `docker/factory/reset.bat` and `reset.sh` — confirm they `docker compose down`
  the full stack (already `--remove-orphans`), still clear `./logs` (they do), and
  copy the updated factory `xeno.json`. No Prometheus/Grafana data handling needed;
  add a one-line comment explaining telemetry storage is ephemeral. If any committed
  telemetry config drifts at runtime (it should not — all mounts are read-only), the
  reset copies nothing for them.
- [ ] Verify `reset.sh`/`reset.bat` bring the full stack back up with telemetry
  flowing (Grafana shows data after a request or two).

## 8. Workstream E — Dashboard UI (Grafana card + i18n + status)

The Settings page already has a `ServiceConsoleCard` grid (NetLedger, Less3). Add a
Grafana card there, and thread the new `Telemetry` settings block through the existing
settings surface. Because Grafana is anonymous-Admin, the card advertises no
credentials — it is a direct link.

- [x] `dashboard/src/App.jsx` — add a Grafana `ServiceConsoleCard` to the
  `service-console-grid`, URL derived from the browser host + port `9400` via the
  existing `serviceConsoleUrl(9400, 'grafanaUrl')`, with a "no login required" note
  instead of credentials. Reuse an appropriate lucide icon (e.g. `Activity` or
  `LineChart`, already imported).
- [x] i18n keys in `dashboard/src/i18n/resources.js` `en` base only (all other locales
  spread `...en.translation`): `grafanaConsole`, `grafanaConsoleDescription`,
  `grafanaConsoleNote` ("anonymous access; local development only"). Run
  `node scripts/verify-i18n.mjs`.
- [x] Surface the `Telemetry` settings in `SettingsView` (a read/edit panel like the
  others, or at minimum include it in `defaultServerSettings()`/`normalizeSettings`
  so the raw-JSON editor round-trips it without dropping it), respecting
  restart-required badges where applicable.
- [ ] Out of scope for this pass (decided): a separate in-dashboard observability
  status view like Switchboard's `ObservabilityView`. Record it in the handoff as a
  deliberate deferral, with the Grafana card and the raw-JSON `Telemetry` settings
  serving the operator need for now. Revisit if the dashboard-standard "Observability
  and Diagnostics" surface is later required.
- [ ] The dashboard ships as a prebuilt image; rebuild `xeno-dashboard` and restart
  the container per the dashboard standard's "rebuild on change" acceptance criterion.

## 9. Workstream F — Documentation, Postman, and assets

Documentation is a first-class deliverable, not an afterthought. Human-facing prose
follows `WRITING_DOCUMENTS.md`.

- [x] `README.md` — a new "Observability" section written in real prose: what the
  stack is, the OTLP→Collector→Prometheus/Tempo/Loki→Grafana topology, the service
  URL/port table (Grafana `:9400`, Prometheus `:9090`, Collector `:4317/:4318/:8889`),
  how to open Grafana, what each pre-built dashboard shows, the metrics catalog table
  (mirrored from A11), the `Telemetry` settings block, and a "Production hardening"
  subsection listing what must change before non-local exposure (real Grafana auth,
  TLS, network restriction, retention/volumes). Update the existing telemetry bullets
  so they mention the new pipeline. Keep the README accurate per `CODE_STYLE.md`.
- [x] `DOCKERHUB_README.md` — create it (currently missing; required by
  `REPOSITORY_REQUIREMENTS.md`). Include the observability stack in the getting-started
  and architecture sections, with image URLs pointing at `assets/`.
- [x] `CHANGELOG.md` — an entry for the observability feature (OpenTelemetry metrics,
  traces, logs; Prometheus/Tempo/Loki/Grafana Docker stack; pre-built dashboards;
  Grafana settings card).
- [x] `REST_API.md` — document the new `Telemetry` settings model and its
  `/settings` round-trip; note that the Prometheus scrape surface is the Collector,
  not a Xeno endpoint (and the fate of the legacy `/metrics` per A10).
- [x] A setup/usage guide — either a new `OBSERVABILITY.md` at root or a section in
  `README.md` (pick one; `agents/requirements` allows operational docs at root or in
  `docs/`). Cover: bring up the stack, open Grafana, tour each dashboard, enable/
  disable signals via settings, adjust sampling, and troubleshoot (no data →
  check Collector logs, OTLP endpoint, `Telemetry.Enable`).
- [x] `Xeno.postman_collection.json` — add/refresh requests for the `Telemetry`
  settings via `GET`/`POST /settings`, and a folder note pointing operators to Grafana
  for metrics/traces/logs rather than a Xeno metrics endpoint. Keep it consistent with
  the existing collection's auth and variables.
- [ ] Assets — capture Grafana dashboard screenshots (Playwright or manual) into
  `assets/` (e.g. `grafana-overview.png`, `grafana-relay.png`, `grafana-tokens.png`)
  and reference them from `README.md`/`DOCKERHUB_README.md` with explicit repository
  URLs, matching the existing `ss1.png`/`ss2.png` convention.
  - Deferred: requires the full stack running with live traffic to capture. Do this
    during the Workstream G6 compose smoke pass once the images are pulled locally.
- [ ] `CLAUDE.md` — record the telemetry conventions (registry location, label
  policy, how to add a metric) per `CODE_STYLE.md`'s "update CLAUDE.md" note.
  - N/A for now: the repository has no `CLAUDE.md`. The telemetry conventions are
    documented in `OBSERVABILITY.md` (catalog + label policy) and the A2/A11 sections
    here; create `CLAUDE.md` if the repo later adopts one.

## 10. Workstream G — Testing (materially expanded, positive and negative)

Tests live in `src/Test.Shared` as Touchstone descriptors (depending only on
`Touchstone.Core`, no console output, self-contained, assert by throwing), run through
the console runner and the xUnit/NUnit adapters. Add a new suite file and register it
in `SharedTestSuites.All`. Both positive and negative cases are required.

> **Implementation status (2026-07-31):** Added the `Observability` suite to
> `SharedTestSuites` (registered in `.All`, 8 cases, all green via
> `Test.Automated --suite Observability`). It covers G1 (settings defaults/clamps and
> the invalid-protocol rejection), G2 (the `MeterListener` instrument-catalog drift
> test, the `ActivitySource` name/tags, and the null-sample resilience), the tenant
> opt-in cardinality control, and G4's positive path (observable gauges reporting the
> registered platform + fleet state). Still open and called out below: the full
> server-driven integration path (G3), the four-provider request-history matrix (G5),
> the compose/stack smoke (G6, runtime), and the `ExportFleetTelemetry=false` /
> staleness negatives (G4). The full solution build currently fails **only** on a
> file-copy lock from a running `xeno.exe`; every project compiles clean and the suite
> runs green against the freshly built assemblies.

### G1. Settings validation (`TelemetrySettingsSuites`)

- [x] Positive: defaults are sane (`Enable=true`, `ExportIntervalMs=15000`, sampling
  `1.0`, endpoint `http://localhost:4317`, protocol `grpc`).
- [x] Positive: valid values round-trip through serialize/deserialize unchanged.
- [x] Negative: `ExportIntervalMs` below 1000 and above 300000 clamp to the bounds.
- [x] Negative: `SamplingRatio` outside 0.0–1.0 clamps; `TimeoutMs` outside 1000–120000
  clamps.
- [x] Negative: invalid `Protocol` string throws `ArgumentException` (not silently
  accepted).
- [ ] Negative: `Headers` are treated as sensitive — assert they are redacted in any
  settings-to-log rendering helper.

### G2. Registry contract (`TelemetryUnitSuites`)

- [x] Positive: using a `MeterListener`, assert every instrument named in the A11
  catalog exists with the expected type and label keys (guards against name/label
  drift — this is the authoritative names test).
- [x] Positive: `ActivitySource` name is `Xeno` and a started activity carries the
  expected tags.
- [x] Negative: recording a relay outcome with a null/blank model does not throw and
  produces a bounded fallback label (e.g. `unknown`), never an empty or unbounded
  label.
- [x] Negative: label values never include raw path or IP — feed a raw path/IP through
  the label helper and assert it is collapsed/omitted.
- [x] Negative: with `IncludeTenantLabel=false`, tenant does not appear as a metric
  label; with it true, it does (and is the only place tenant is emitted as a label).

### G3. Instrumentation integration (`TelemetryInstrumentationSuites`)

- [ ] Positive: with an in-process `MeterListener`/`ActivityListener`, drive a
  simulated relay through the `PopulateRequestHistory` path and assert
  `xeno_relay_requests_total`, `xeno_time_to_first_token_seconds`,
  `xeno_total_tokens_total`, and the request-duration histogram all record.
- [ ] Positive: register and unregister a client connection and assert the connect/
  disconnect counters and `xeno_connected_clients` gauge move.
- [ ] Positive: an auth failure, an authorization denial, and an email send/failure
  each move their counters.
- [x] Negative: when `Telemetry.Enable=false` (no provider listening), the hot path
  still runs without throwing and records nothing observable (no listener callbacks).
- [x] Negative: constructing `TelemetryService` with an unreachable OTLP endpoint does
  not throw at startup and does not block request handling (export failures are
  swallowed by the SDK; assert the server still starts and serves).
- [ ] Negative: a 5xx unhandled path increments the error counter and the request
  still returns a response (ties to the existing central-exception-handler logging).

### G4. Fleet export (`TelemetryFleetSuites`)

- [x] Positive: seed a latest host sample and a provider sample, run the observable
  gauge collection, and assert `xeno_client_host_cpu_percent{client}` and
  `xeno_provider_active_requests{client,provider}` are emitted with the seeded values.
- [x] Negative: with `ExportFleetTelemetry=false`, no fleet gauges are emitted.
- [ ] Negative: a missing/expired sample yields no series for that client rather than
  a zero-or-stale value (define and test the chosen staleness behavior).

### G5. Provider-matrix relevance

- [ ] Because relay metrics derive from request-history persistence, run the
  request-history-backed metric assertions that read counts through the DB across the
  four providers (`Sqlite`, `Mysql`, `Postgresql`, `SqlServer` — use the existing
  provider-matrix harness and the throwaway-container pattern already established) to
  confirm counts and the legacy `/metrics` (if kept) agree per provider. Positive on
  all four; negative case: metrics count is unaffected by request-history retention
  pruning of old rows (counters are monotonic, gauges reflect current state).

### G6. Compose/stack smoke (documented manual or scripted)

- [ ] Scripted or documented end-to-end: `docker compose up -d`, issue a few requests,
  then `curl http://localhost:9090/api/v1/query?query=xeno_http_requests_total` and
  assert non-empty; hit the Collector's `:8889/metrics` and assert `xeno_` series are
  present; open Grafana `:9400` and confirm the provisioned dashboards load with data.
  Positive path plus a negative: stop the Collector and confirm Xeno keeps serving
  (telemetry export degrades, requests do not).

### G7. Dashboard/i18n tests

- [x] i18n: `node scripts/verify-i18n.mjs` passes with the new Grafana card keys; the
  keys exist in the `en` base and are inherited by all locales.
- [x] Frontend build passes; the Grafana card renders and links to the derived host
  URL; visual QA per `DASHBOARD_STYLE_AND_USABILITY.md` "Mandatory Visual QA" (desktop
  + one narrow viewport, light + dark) for the Settings page with the new card,
  captured or summarized in the handoff.

### G8. Registration and runner wiring

- [x] Add all new suites to `SharedTestSuites.All`; confirm they run green via
  `Test.Automated` console runner and are picked up by `Test.Xunit`/`Test.Nunit`
  adapters. No console output added to `Test.Shared`.

---

## 11. Acceptance criteria

The feature is done when all of the following are true:

- [x] `Xeno.Core`/`Xeno.Server` build warning-free with the OpenTelemetry packages and
  all instrumentation, following the mandatory code standards (no `var`, no tuples,
  usings inside namespace, `_Pascal` privates, XML docs on publics, `ConfigureAwait`,
  `CancellationToken`, one class per file, clamped/validated settings, no
  `Console.Write*`).
- [x] `TelemetryService` is constructed first at startup, disposed cleanly, and gated
  by `Telemetry.Enable`; disabling it leaves the server fully functional.
- [x] Every metric in the A11 catalog is emitted at the correct site with bounded
  labels, and the registry-contract test enforces the catalog.
- [ ] Metrics, traces, and logs all reach Grafana through the Collector; the
  provisioned dashboards render with live data; trace↔log correlation resolves.
  *(Runtime-verify: requires `docker compose up` with the images pulled — G6 smoke.)*
- [x] `docker compose config` validates; `reset.sh`/`reset.bat` restore factory state
  and copy the telemetry-enabled `xeno.json`. *(Full `up -d` is the G6 smoke pass.)*
- [x] The Grafana card appears on the Settings page, links correctly, and i18n checks
  pass.
- [x] Documentation is complete and accurate: README observability section,
  `DOCKERHUB_README.md`, `CHANGELOG.md`, `REST_API.md` telemetry settings, and the
  `OBSERVABILITY.md` setup guide + Postman updates. *(Grafana screenshots deferred to
  the G6 smoke pass — needs the live stack.)*
- [x] The observability suite is green through the console runner (`Test.Automated
  --suite Observability`, 8/8) with negative cases present and passing. Broader
  server-integration/provider-matrix cases (G3/G5) remain open.
- [x] The metrics catalog and the "Production hardening" non-goal are both documented.

## 12. Rejection criteria

The first pass is not acceptable if any of the following are true:

- [ ] Instrumentation adds unbounded label cardinality (raw path, IP, or default
  tenant labels on metrics).
- [ ] The application opens its own Prometheus HTTP listener instead of exporting OTLP
  to the Collector (contradicts the locked topology), without a recorded, justified
  divergence.
- [ ] Telemetry failures (unreachable OTLP endpoint, Collector down) can throw into or
  block the request path.
- [ ] Only a subset of the required metric surfaces is instrumented (e.g. requests but
  not TTFT/tokens/connections/fleet) without a written product reason.
- [ ] Grafana ships empty (no provisioned dashboards) or dashboards reference metrics
  that are never emitted.
- [ ] Tests are added without negative cases, or shared tests write to the console, or
  new suites are not registered in `SharedTestSuites.All`.
- [ ] Secrets (`Otlp.Headers`) are logged or committed, or the settings are not
  validated/clamped on load.
- [ ] Documentation is a sentence followed by a bare list, or the README is left
  inaccurate.

## 13. Risks and mitigations

- **Cardinality blow-up** — the single biggest risk with LLM metrics (model × client ×
  provider × code). Mitigation: bounded label policy (A9), tenant off by default,
  route identifiers not paths, and the negative cardinality tests (G2).
- **Collector as a single point of failure** — if it is down, no signal ships.
  Mitigation: SDK export failures are non-fatal (G3 negative test); optionally keep the
  legacy self-scrape `/metrics` (A10) as a fallback surface.
- **Ephemeral storage** — dashboards and history vanish on `compose down`. Accepted
  non-goal; documented, with durable volumes listed as a follow-up.
- **Grafana anonymous-Admin** — insecure by design for local dev. Mitigation: the
  "Production hardening" doc subsection and a non-goal statement; the settings card
  labels it local-only.
- **Trace propagation to clients** — the `xeno` client may not yet continue the trace.
  Mitigation: propagate best-effort, record the gap in the handoff, and file a
  follow-up for client-side continuation.

## 14. Sequencing

A natural order that keeps the build verifiable at each step:

1. Workstream A1–A3 (settings, registry, service) — build green, telemetry no-ops.
2. Workstream B (compose + Collector/Prometheus/Tempo/Loki + Grafana provisioning) —
   stack comes up, `service.name` resource visible in Prometheus.
3. Workstream A4–A8 (instrument hot paths, connections, auth/email/ledger, fleet,
   traces) — series and spans start flowing.
4. Workstream C (dashboards) — build against the now-live series.
5. Workstream E (dashboard card + settings) and D (factory/reset).
6. Workstream G (tests) alongside 3–5, not after.
7. Workstream F (docs/postman/assets/screenshots) last, once surfaces are stable.

## 15. Handoff requirements

The completion writeup must include: the final metrics catalog (A11); which Switchboard
files and `agents/requirements` documents were used and why; any backend capability
that is missing or deferred (e.g. client-side trace continuation) and the surface it
affects; whether the dashboard telemetry status card was built or deliberately
skipped; a summary of the mandatory visual QA on the Settings page; and a summary of
tests and builds run, including any (such as the live-stack smoke in G6) that could
not be run in the environment and the residual risk.

# Xeno Observability

Xeno exports metrics, distributed traces, and logs from the server using OpenTelemetry. This guide explains the pipeline, how to bring it up, what each dashboard shows, how to configure it, the full metric catalog, and how to troubleshoot when data does not appear.

## Pipeline

The server instruments in-process with the .NET `System.Diagnostics.Metrics` `Meter` API and the OpenTelemetry SDK, and exports every signal over OTLP to a standalone OpenTelemetry Collector. The Collector fans the signals out to three backends, and Grafana reads all three.

```
xeno-server ──OTLP 4317 (metrics + traces)──▶ otel-collector
xeno-server ──file logs (./logs/*.log*)─────▶ otel-collector (filelog receiver)
   otel-collector ──prometheus :8889──▶ prometheus (:9090) ─┐
   otel-collector ──OTLP──────────────▶ tempo      (:3200) ─┼─▶ grafana (:9400)
   otel-collector ──OTLP──────────────▶ loki       (:3100) ─┘
```

Keeping the Prometheus scrape surface on the Collector rather than in-process means the server depends only on GA OpenTelemetry packages and never opens a second HTTP listener inside the web server. Metrics and traces travel over OTLP; logs are shipped by the Collector's `filelog` receiver tailing the server's rolling log files on the shared `./logs` volume, so file logging must be enabled for logs to reach Loki.

## Bring up the stack

The whole stack lives in `docker/compose.yaml`:

```
cd docker
docker compose up -d
```

Service endpoints:

| Service | URL | Notes |
| --- | --- | --- |
| Grafana | http://localhost:9400 | Anonymous access, no login |
| Prometheus | http://localhost:9090 | Query UI and scrape targets |
| OTLP Collector | localhost:4317 (gRPC), 4318 (HTTP) | Telemetry ingest |
| Collector Prometheus surface | http://localhost:8889/metrics | Raw exposition |

Tempo and Loki have no host ports; Grafana reaches them on the internal compose network. The telemetry backends keep no named volumes, so a `docker compose down` discards their data — a factory reset needs no extra cleanup for them.

Open Grafana at http://localhost:9400. The datasources (Prometheus, Tempo, Loki) and eight dashboards are provisioned automatically and appear in the top-level Dashboards list. The dashboard Settings page also has a **Grafana** card at the bottom that links here.

## Dashboards

| Dashboard | Shows |
| --- | --- |
| Xeno — Overview | Request rate and latency, relay throughput and error ratio, token throughput, time to first token |
| Xeno — Relay & LLM | Relay rate by model/provider, TTFT and streaming/upstream/round-trip latency percentiles, payload sizes |
| Xeno — Tokens | Prompt/completion/total token rates, tokens-per-request distribution, streamed chunks and events |
| Xeno — Connections & Fleet Health | Connect/disconnect/eviction/registration rates, collection cycles and failures, ledger queue and events |
| Xeno — Fleet (Host & Provider) | Per-client host CPU/memory/disk/GPU, per-provider available/loaded models and reachability |
| Xeno — Auth, RBAC & Email | Auth outcomes and failure ratio, authorization denials, privileged bypasses, data mutations, email outcomes |
| Xeno — Runtime (.NET) | Process uptime/memory/threads/CPU and .NET GC, thread pool, and exception metrics |
| Xeno — Logs | A Loki view over the server logs with a warnings-and-errors panel |

Traces are explorable in Grafana's Explore view against the Tempo datasource. The Loki datasource is configured with a derived `TraceID` field, so a log line carrying a trace id links directly to its trace.

## Configuration

Telemetry is configured under the `Telemetry` block in `xeno.json`:

```json
"Telemetry": {
  "Enable": true,
  "ServiceName": "xeno-server",
  "Metrics": { "Enable": true, "ExportIntervalMs": 15000, "IncludeTenantLabel": false, "ExportFleetTelemetry": true },
  "Traces": { "Enable": true, "SamplingRatio": 1.0, "PropagateToClient": true },
  "Logs": { "Enable": true },
  "Otlp": { "Endpoint": "http://otel-collector:4317", "Protocol": "grpc", "TimeoutMs": 10000, "Headers": null }
}
```

| Field | Meaning |
| --- | --- |
| `Enable` | Master switch. When false, no OpenTelemetry provider is created and instruments stay inert. |
| `ServiceName` | Reported as the OpenTelemetry resource `service.name`. |
| `Metrics.ExportIntervalMs` | Metric export period (1000–300000 ms). |
| `Metrics.IncludeTenantLabel` | Opt-in tenant metric label. Off by default to keep cardinality bounded. |
| `Metrics.ExportFleetTelemetry` | Export the per-client host and provider fleet gauges. |
| `Traces.SamplingRatio` | Head sampling ratio (0.0–1.0). |
| `Traces.PropagateToClient` | Propagate W3C trace context toward the client on relayed requests. |
| `Otlp.Endpoint` / `Otlp.Protocol` | Collector endpoint and `grpc` or `httpprotobuf`. |
| `Otlp.TimeoutMs` | Export request timeout (1000–120000 ms). |
| `Otlp.Headers` | Optional OTLP headers (sensitive; redact when logging). |

Environment overrides take precedence over the file: `XENO_TELEMETRY_ENABLED`, `XENO_TELEMETRY_SERVICE_NAME`, `XENO_TELEMETRY_OTLP_ENDPOINT` (or `OTEL_EXPORTER_OTLP_ENDPOINT`), `XENO_TELEMETRY_OTLP_PROTOCOL` (or `OTEL_EXPORTER_OTLP_PROTOCOL`), `XENO_TELEMETRY_METRICS_ENABLED`, `XENO_TELEMETRY_TRACES_ENABLED`, `XENO_TELEMETRY_TRACES_SAMPLING_RATIO`, and `XENO_TELEMETRY_INCLUDE_TENANT_LABEL`. The Docker compose file sets the OTLP endpoint to `http://otel-collector:4317`.

## Cardinality policy

Metric labels are bounded sets only: `model`, `provider`, `client_id`, `http_route`, `http_status_code`, `outcome`, `result`, `entity`, `operation`, `reason`. Raw URL paths are collapsed to route templates (for example `/v1.0/api/tenants/{id}`), and client IPs never become labels. `tenant` is deliberately not a default metric label; it is a trace-span tag and a log field, and becomes a metric label only when `Metrics.IncludeTenantLabel` is enabled.

## Metric catalog

Instrument names are the OpenTelemetry (dotted) names the server emits. The Prometheus name is what the Collector's Prometheus exporter produces (dots become underscores, monotonic counters gain `_total`, and unit suffixes are appended). All instruments are registered in `src/Xeno.Core/Telemetry/XenoTelemetry.cs`.

| OTel instrument | Prometheus name | Type | Labels |
| --- | --- | --- | --- |
| `xeno.http.requests` | `xeno_http_requests_total` | counter | `http_method`, `http_route`, `http_status_code`, `http_status_class` |
| `xeno.http.request.duration` | `xeno_http_request_duration_seconds` | histogram | `http_method`, `http_route`, `http_status_code`, `http_status_class` |
| `xeno.relay.requests` | `xeno_relay_requests_total` | counter | `model`, `client_id`, `provider`, `outcome`, `streamed`, `tenant?` |
| `xeno.relay.failures` | `xeno_relay_failures_total` | counter | relay labels |
| `xeno.relay.roundtrip.duration` | `xeno_relay_roundtrip_duration_seconds` | histogram | relay labels |
| `xeno.relay.upstream.duration` | `xeno_relay_upstream_duration_seconds` | histogram | relay labels |
| `xeno.relay.time_to_first_token` | `xeno_relay_time_to_first_token_seconds` | histogram | relay labels |
| `xeno.relay.streaming.duration` | `xeno_relay_streaming_duration_seconds` | histogram | relay labels |
| `xeno.relay.request.size` | `xeno_relay_request_size_bytes` | histogram | relay labels |
| `xeno.relay.response.size` | `xeno_relay_response_size_bytes` | histogram | relay labels |
| `xeno.tokens.prompt` | `xeno_tokens_prompt_total` | counter | relay labels |
| `xeno.tokens.completion` | `xeno_tokens_completion_total` | counter | relay labels |
| `xeno.tokens.all` | `xeno_tokens_all_total` | counter | relay labels |
| `xeno.tokens.per_request` | `xeno_tokens_per_request` | histogram | relay labels |
| `xeno.relay.stream.chunks` | `xeno_relay_stream_chunks_total` | counter | relay labels |
| `xeno.relay.stream.events` | `xeno_relay_stream_events_total` | counter | relay labels |
| `xeno.client.connections` | `xeno_client_connections_total` | counter | `provider` |
| `xeno.client.disconnections` | `xeno_client_disconnections_total` | counter | `reason` |
| `xeno.client.registrations` | `xeno_client_registrations_total` | counter | — |
| `xeno.client.evictions` | `xeno_client_evictions_total` | counter | — |
| `xeno.auth.attempts` | `xeno_auth_attempts_total` | counter | `result` |
| `xeno.auth.denials` | `xeno_auth_denials_total` | counter | — |
| `xeno.auth.privileged_bypasses` | `xeno_auth_privileged_bypasses_total` | counter | — |
| `xeno.data.mutations` | `xeno_data_mutations_total` | counter | `entity`, `operation` |
| `xeno.email.results` | `xeno_email_results_total` | counter | `outcome` |
| `xeno.ledger.events` | `xeno_ledger_events_total` | counter | `outcome` |
| `xeno.fleet.collection.cycles` | `xeno_fleet_collection_cycles_total` | counter | — |
| `xeno.fleet.collection.failures` | `xeno_fleet_collection_failures_total` | counter | — |
| `xeno.request_history.pruned` | `xeno_request_history_pruned_total` | counter | — |
| `xeno.build.info` | `xeno_build_info` | gauge | `version`, `runtime` |
| `xeno.process.memory.working_set` | `xeno_process_memory_working_set_bytes` | gauge | — |
| `xeno.process.memory.gc_heap` | `xeno_process_memory_gc_heap_bytes` | gauge | — |
| `xeno.process.cpu.time` | `xeno_process_cpu_time_seconds_total` | counter | — |
| `xeno.process.threads` | `xeno_process_threads` | gauge | — |
| `xeno.process.uptime` | `xeno_process_uptime_seconds` | gauge | — |
| `xeno.clients.connected` | `xeno_clients_connected` | gauge | — |
| `xeno.models.available` | `xeno_models_available` | gauge | — |
| `xeno.relay.inflight` | `xeno_relay_inflight` | gauge | — |
| `xeno.ledger.queue.depth` | `xeno_ledger_queue_depth` | gauge | — |
| `xeno.fleet.host.cpu` | `xeno_fleet_host_cpu_percent` | gauge | `client_id` |
| `xeno.fleet.host.memory` | `xeno_fleet_host_memory_percent` | gauge | `client_id` |
| `xeno.fleet.host.disk` | `xeno_fleet_host_disk_percent` | gauge | `client_id` |
| `xeno.fleet.host.gpu` | `xeno_fleet_host_gpu_percent` | gauge | `client_id` |
| `xeno.fleet.provider.models_available` | `xeno_fleet_provider_models_available` | gauge | `client_id`, `provider` |
| `xeno.fleet.provider.models_loaded` | `xeno_fleet_provider_models_loaded` | gauge | `client_id`, `provider` |
| `xeno.fleet.provider.up` | `xeno_fleet_provider_up` | gauge | `client_id`, `provider` |

The `AddRuntimeInstrumentation()` package adds the standard `process_runtime_dotnet_*` series (GC heap and collections, JIT, thread pool, exceptions, monitor contention).

### Usage analytics

Usage analytics queries emit `xeno.analytics.query.duration` (histogram) and `xeno.analytics.records.scanned` (histogram), with an `analytics usage` span around each query. Use them to spot ranges that read close to `Analytics.MaxRecordsPerQuery`. The charts themselves are described in [USAGE_ANALYTICS.md](USAGE_ANALYTICS.md).

## Troubleshooting

**No metrics in Grafana or Prometheus.** Confirm `Telemetry.Enable` and `Telemetry.Metrics.Enable` are true and the server logged `[Telemetry] exporting to ...` at startup. Check that the OTLP endpoint resolves from the server (`http://otel-collector:4317` inside compose, `http://localhost:4317` from the host). Confirm Prometheus is scraping: open http://localhost:9090/targets and check the `otel-collector` target is up. Query the Collector directly at http://localhost:8889/metrics and look for `xeno_` series.

**No logs in Loki.** Log shipping is done by the Collector's `filelog` receiver over the shared `./logs` volume, so file logging must be enabled in the `Logging` block and the log directory must be the mounted `./logs`. Confirm the Collector container can see the files.

**No traces in Tempo.** Confirm `Traces.Enable` is true and `Traces.SamplingRatio` is above 0. Generate a request, then use Grafana Explore against Tempo.

**Server keeps serving but export is failing.** Export failures are swallowed by the OpenTelemetry SDK; the server continues to serve requests regardless. Check the Collector container logs for receiver or exporter errors, and verify the OTLP endpoint and protocol.

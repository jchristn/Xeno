<img src="https://raw.githubusercontent.com/jchristn/Xeno/main/assets/logo.png" alt="Xeno" width="120" />

# Xeno

Xeno is a multi-tenant control plane for a fleet of LLM inference servers. It presents a single OpenAI-compatible API in front of many model-serving clients — contributor GPUs, private data-center accelerators, and hybrid provider capacity — and handles tenant isolation, authentication and RBAC, model-aware routing and load balancing, request history, token ledgering, and full observability.

Two images make up a deployment:

- **`jchristn77/xeno-server`** — the control-plane server (REST + OpenAI-compatible API + WebSocket client channel).
- **`jchristn77/xeno-dashboard`** — the web dashboard.

The Compose stack also wires in NetLedger (token ledgering), Less3 (S3-compatible archive storage), PostgreSQL, and a turnkey OpenTelemetry observability stack (Prometheus, Tempo, Loki, and Grafana).

## When to use it

Reach for Xeno when you have more than one machine serving models and want one authenticated, OpenAI-compatible front door across all of them, with tenant separation, per-request history and token accounting, and dashboards over the whole fleet. Model-serving clients connect out to the server over an authenticated WebSocket and advertise their models; callers hit the server's OpenAI-compatible endpoints and Xeno routes each request to a healthy client that serves the requested model.

## What's inside

- OpenAI-compatible API (`/v1/models`, chat/completions, embeddings) with tenant-scoped inference permissions
- Model-aware routing and load balancing (round-robin and adaptive) across health-checked clients
- Opt-in cross-tenant shared clients: a node owned by one tenant can serve others, with owner-set sharing, provenance labels, redacted foreign views, and a per-tenant consume switch
- Multi-tenant identity: tenants, users, credentials, roles, permissions, and role assignments with grantor-ceiling RBAC
- Fleet client channel over WebSocket with heartbeats, inventory advertisement, and stale-connection eviction
- Server-collected client host and provider telemetry (CPU/memory/disk/GPU, model counts, health)
- Request history with retention and per-request timings (time to first token, streaming duration, tokens, round-trip and upstream timing)
- Usage analytics: latency percentiles, time to first token, throughput, tokens, heatmap, time by stage, provider health, and connected clients over time
- NetLedger-backed token ledgering with tenant lifecycle cascade
- Full OpenTelemetry observability — metrics, distributed traces, and logs over OTLP — with a bundled Prometheus + Tempo + Loki + Grafana stack and eight pre-built dashboards (Grafana on `:9400`)
- SQLite, MySQL, PostgreSQL, or SQL Server for control-plane storage
- OpenAPI document, management REST API, and a full web dashboard with internationalization

## Quick start

The turnkey way is the Compose stack, which brings up the server, dashboard, Postgres, NetLedger, Less3, and the observability stack together:

```bash
git clone https://github.com/jchristn/Xeno
cd Xeno/docker
docker compose up -d
```

Then open:

- Dashboard — http://localhost:9100
- Xeno API / health — http://localhost:9000/health
- Grafana dashboards — http://localhost:9400 (anonymous access, no login)
- Prometheus — http://localhost:9090

To run the server image on its own against your own `xeno.json`:

```bash
docker run -d --name xeno-server -p 9000:9000 \
  -v "$(pwd)/xeno.json:/app/xeno.json" \
  -v "$(pwd)/logs:/app/logs" \
  jchristn77/xeno-server:v0.4.0
```

## Architecture

```
callers ──OpenAI-compatible API──▶ xeno-server ◀──WebSocket──── model-serving clients (GPU hosts)
                                        │
              request history + RBAC ───┼─── token ledgering (NetLedger + Less3 archive)
                                        │
                     OTLP (metrics + traces), file logs
                                        ▼
                    otel-collector ──▶ Prometheus / Tempo / Loki ──▶ Grafana (:9400)
```

## Observability

The server instruments with OpenTelemetry and exports metrics, traces, and logs over OTLP to the bundled Collector, which feeds Prometheus, Tempo, and Loki. Grafana is provisioned with datasources and eight dashboards (overview, relay/LLM, tokens, connections, fleet, auth/RBAC/email, .NET runtime, and logs). It ships anonymous-Admin for local use; harden authentication, TLS, and network exposure before running it beyond a trusted network. See [OBSERVABILITY.md](https://github.com/jchristn/Xeno/blob/main/OBSERVABILITY.md).

## Documentation

Full documentation, the REST API reference, SDKs (C#, JavaScript, Python), and the Postman collection are in the [GitHub repository](https://github.com/jchristn/Xeno).

## License

MIT. See [LICENSE.md](https://github.com/jchristn/Xeno/blob/main/LICENSE.md).

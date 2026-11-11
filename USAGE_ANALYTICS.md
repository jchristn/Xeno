# Xeno Usage Analytics

Usage analytics answers how much traffic Xeno handled, how fast it was, how many tokens it moved, and where the time went. The backend computes every number, and each surface only draws the result, so the same data looks the same in the local web dashboard, the server web dashboard, the desktop app, the terminal app, `xeno usage`, the `xeno_usage` MCP tool, and the SDKs.

## Where to find it

| Surface | Where |
|---|---|
| Local web dashboard | **Usage**, first under Activity |
| Server web dashboard | **Usage**, right after Home (the old `/dashboard/analytics` link redirects here) |
| Desktop app | **Usage**, first under Activity |
| Terminal app | **Usage**, first under Activity. One chart at a time, picked with the Chart dropdown |
| CLI | `xeno usage [--dataset served\|endpoint\|chat] [--range hour\|day\|week\|month] [--group-by model\|provider\|route] [--metric requests\|tokens] [--model <name>] [--json]` |
| MCP | `xeno_usage` (see [MCP.md](MCP.md)) |
| SDKs | C#, JavaScript, and Python clients under `src/sdk` |
| REST | See [REST_API.md](REST_API.md) |

The client surfaces need the Xeno client running, because they read its local API.

## Datasets

| Dataset | Where | What it counts |
|---|---|---|
| Served requests | Client | Network requests this machine answered |
| Local endpoint | Client | Requests to the local inference listener (see [HYBRID_MODELS.md](HYBRID_MODELS.md)) |
| Chat | Client and server | Chat turns |
| Network requests | Server | Inference requests the server routed |

Request datasets count inference paths only: `/v1/` routes other than model listings, and Ollama's `/api/chat`, `/api/generate`, `/api/embed`, and `/api/embeddings`.

## Ranges

| Range | Buckets |
|---|---|
| Last hour | 60 of 1 minute |
| Last day | 96 of 15 minutes |
| Last week | 84 of 2 hours |
| Last month | 120 of 6 hours |

Every page opens on the last day. Charts refresh on a choice of off, 15 seconds, or 60 seconds (15 by default). An open dialog pauses the refresh.

## Charts

| Chart | Shows | How to read it |
|---|---|---|
| Requests by outcome | Succeeded, network fallback, and failed per bucket | Fallback appears only for datasets that record it |
| Latency | Median, 95th, 99th percentile, and slowest | A wide gap between median and 95th means a slow tail |
| Time to first token | The same percentiles | What a person waits before text appears |
| Streaming time | The same percentiles | How long the answer took after its first token |
| Throughput | Median and 95th percentile tokens per second | Only requests that streamed for at least 50 ms count, because shorter ones produce meaningless rates |
| Tokens | Prompt, cached prompt, and completion | Cached prompt tokens are recorded for chat only |
| By group over time | The top groups per bucket, the rest folded into Other | Group by model, provider, or route; the server adds client, and for administrators user, credential, and tenant |
| Group totals | The same groups for the whole range | |
| Token heatmap | Tokens by time cell | See below |
| Time by stage | Each stage per bucket | See below |
| Average time by stage | Each stage for the whole range | |
| Feedback (chat) | Thumbs up and down | |
| Tool calls (chat) | Succeeded, failed, and denied | |

The server's Usage page also charts connected clients. The server records each tenant's connected and registered client counts once a minute and keeps them for 30 days. A bucket takes its last sample, and a bucket with no sample stays empty because the server was not running then.

Provider health appears as a strip on the provider telemetry pages: one cell per bucket (healthy, degraded, unhealthy, or no data), with uptime and the current failure streak. Request details show the same stages as timing bars.

Host telemetry charts any of seven metrics, and provider telemetry charts any provider metric filtered by provider and model. Both draw average, minimum, and maximum lines.

## Stages

| Stage | Dataset | Formula |
|---|---|---|
| Server overhead | Network requests | Request duration minus client round trip |
| Relay and client | Network requests | Client round trip minus upstream total |
| Provider headers | Network, served | Request start to response headers |
| Provider first token | Network, served | Time to first token minus time to headers |
| Queue | Local endpoint | Time waiting for a provider slot |
| Provider first token | Local endpoint | First byte, measured after the queue. Time to first token is queue plus first byte |
| Streaming | Request datasets | First token to the end of the response |
| Model first token | Chat | Time to first token |
| Model streaming | Chat | Model time minus time to first token |
| Tools | Chat | Time running tools, excluding time waiting for approval |
| Approval | Chat | Time waiting for someone to allow or deny a tool |
| Other | Chat | Total minus model, tools, and approval |

A stage is left out when its inputs were not recorded, so stages can sum to less than the total.

## Heatmap layouts and time zones

| Range | Layout |
|---|---|
| Up to 2 hours | One row per 10 minutes, one column per minute |
| Up to 2 days | One row per day, one column per hour |
| Longer | One row per weekday (Sunday first), one column per hour |

Hour and weekday cells use the viewer's offset from UTC (`utcOffsetMinutes`), applied as one fixed offset for the whole range. A range that crosses a daylight saving change is off by an hour on one side of it.

## Percentiles and truncation

Percentiles are exact and use the nearest-rank method on the records in the bucket. Nothing averages percentiles across buckets.

A query reads at most `MaxRecordsPerQuery` records (250,000 by default). Past that limit the response sets `Truncated` and `TruncatedReason`, and every surface shows the reason. The same notice appears briefly after an upgrade while older rows are indexed.

## Drill-down

Clicking a bucket opens the matching list filtered to that time window, and to the model when the chart is grouped by model.

| Surface | How |
|---|---|
| Web | Click a bucket, or focus it and press Enter |
| Desktop | Click a bucket, or use the arrow keys and Enter |
| Terminal | Arrow keys and Enter, or click once to select a bucket and again to open it |

Served requests open Requests, the local endpoint opens Local endpoint, chat opens Chat history, and network requests open Request History.

## Who can see what

Server analytics are scoped to the caller's tenant. Grouping or filtering by user or credential needs a tenant administrator. Grouping by tenant needs a global administrator. Chat usage covers the caller's own turns unless a tenant administrator passes `scope=tenant`. Prompt and answer text is never part of analytics.

## Settings

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `Analytics.MaxRecordsPerQuery` | 250000 | 10,000 to 5,000,000 | Records one query reads before it reports truncation |
| `Analytics.DefaultTopN` | 8 | 1 to 20 | Groups shown by name before the rest fold into Other |
| `LocalInferenceServer.RequestRetentionDays` | 30 | 1 to 365 | How long the client keeps local endpoint usage rows |

`Analytics` exists in both the server's `xeno.json` and the client's `client.json`.

## Implementation

`UsageAnalyticsBuilder` (`src/Xeno.Core/Analytics`) computes the analytics, and `XenoChartCatalog` (`src/Xeno.Client.Core/Presentation`) defines each chart. The catalog is exported to `dashboard/src/charts/chartCatalog.json` so the web dashboards follow the same definitions. To regenerate it, run `XENO_WRITE_CHART_CATALOG=1 dotnet run --project src/Test.Automated --no-build -- --suite UsageCharts`.

# Reporting Enhancements Plan

Status: draft for review, 2026-11-10. Nothing in this plan has been built.

Xeno counts requests well and says very little about them. A user can see that 400 requests succeeded in the last hour, but not how long the slow ones took, how many tokens moved, which model carried the load, or which stage of the trip was slow. Every sibling product that serves models charts at least some of that, and Mux charts most of it on all three of its surfaces. This plan closes the gap for every Xeno surface at once: the local web dashboard, the server web dashboard, the desktop app, and the terminal app. It also covers everything those charts depend on or are documented by: the backend, storage, the CLI, MCP, the three SDKs, Postman, the OpenAPI document, the API explorers, and the documentation.

The chart choices come from `~/Code/REPORTING_MATRIX.md`, which catalogs every usage chart in Wilson, Pneuma, AssistantHub, Mux, Armada, and Xeno. Chart identifiers below (`R02` and so on) refer to that catalog. The plan follows `~/Code/Agents/requirements`; where those requirements conflict with each other or with how Xeno already works, the conflict is listed under [Decisions to settle first](#decisions-to-settle-first) with a recommended answer, rather than resolved silently.

## What gets built

Fourteen charts, plus two detail views. All of them appear on every surface; none is web-only or desktop-only.

| # | Chart | Catalog | What it shows | Datasets |
|---|---|---|---|---|
| C1 | Requests by outcome, with fallback | R01 + fallback | Stacked columns: succeeded, failed, and (where known) answered by a network fallback | All |
| C2 | Latency percentiles | R02, R04 | Lines per bucket: p50, p95, p99, and maximum. A metric choice switches between total time and streaming time | All |
| C3 | Time to first token | R03 | Lines per bucket: p50, p95, p99, and maximum time to first token | All |
| C4 | Throughput | R05 | Line per bucket: median and p95 output tokens per second | All |
| C5 | Tokens by type | R06 | Stacked columns: uncached prompt, cached prompt (where recorded), and completion tokens | All |
| C6 | Activity by group | R07, R20 | Stacked columns of requests or tokens per bucket, one series per group value, top N plus Other. Group by model, provider, or route everywhere; by client, tenant, user, or credential on the server | All |
| C7 | Totals by group | R08 | Horizontal bars of requests or tokens per group value for the whole range | All |
| C8 | Token activity heatmap | R09 | Grid of cells shaded by tokens; the layout follows the range | All |
| C9 | Timing by stage over time | R13 | Stacked columns of average milliseconds per stage per bucket | All |
| C10 | Average time by stage | R14 | Horizontal bars of average and p95 milliseconds per stage for the range | All |
| C11 | Feedback trend | R12 | Stacked columns of thumbs up and thumbs down per bucket | Chat |
| C12 | Tool calls | R17 | Stacked columns of tool calls per bucket: succeeded, failed, denied | Chat |
| C13 | Provider health strip | R18 | One cell per bucket per provider: healthy, degraded, unhealthy, or no data, with uptime percentage | Provider telemetry |
| C14 | Drill down from a bucket | R21 | Clicking or selecting any time bucket opens that dataset's list filtered to the bucket (and to the series, when the series is a group value) | All time charts |
| V1 | Request timing bars | R15 | Horizontal bars for one request or chat turn: milliseconds per stage and share of the total | Detail views |
| V2 | Usage KPIs | (tiles) | Requests, error rate, p50 and p95 latency, p50 time to first token, tokens in and out, median tokens per second | All |

A dataset is one stream of records that can be charted. Xeno has five, and each surface charts the ones it can see:

| Dataset | Records | Where it lives | Surfaces |
|---|---|---|---|
| Network requests | Requests the Xeno server relayed | Server `request_history` (SQLite, PostgreSQL, MySQL, SQL Server) | Web-S |
| Served requests | Network requests this machine answered for the Xeno network | Local dashboard database `local_request_history` | Web-L, desktop, terminal |
| Local endpoint | Requests to the local inference listener (port 11435) | Today a 1000-line JSON log; Phase 2 moves it into the local dashboard database | Web-L, desktop, terminal |
| Chat | Chat turns | Local chat store, and the server chat store per tenant and user | All four (server chat on Web-S) |
| Provider telemetry | Provider health and metric samples | Local dashboard database, and the server per client | All four |

The charts live on a new **Usage** page on every surface, with a dataset picker, plus the health strip on each surface's provider telemetry page and the timing bars in each request and chat-turn detail view. Putting fourteen charts on the existing Requests and Chat history pages would bury both; one Usage page keeps those pages fast and gives the terminal app a single place to cycle through charts.

## Decisions to settle first

Each of the following needs an answer before Phase 1, because they change file layouts, bucket math, or storage. Each has a recommendation; if the recommendation is accepted, no further discussion is needed.

**D1. Bucket presets.** `FRONTEND_ARCHITECTURE.md` ("ActivityChart") fixes the presets at hour 1 minute x 60, day 15 minutes x 96, week 2 hours x 84, month 6 hours x 120, and says range presets must use "the exact bucket counts above". Xeno uses 8 hours x 90 for the month on every chart, and chat uses 30 seconds x 120 for the hour. *Recommendation:* adopt the required presets for every chart, new and existing, in `HistoryRanges` and `dashboard/src/chat/chatRanges.js`, so that every chart on a page lines up bucket for bucket. The 2026-11-10 chat bucket choice is superseded; `bucketSeconds` stays accepted on the chat routes for compatibility.

**D2. Route prefixes.** `BACKEND_ARCHITECTURE.md` says "Use `/api/v1/...` versioned paths" and, in the same file, mandates `/v1.0/api/request-history`. *Recommendation:* keep Xeno's existing prefixes: `/v1.0/api/...` on the server and `/api/v1/...` on the local dashboard. New routes sit next to the ones they extend.

**D3. Structured persistence.** `BACKEND_ARCHITECTURE.md` ("Structured Persistence Rule") names Xeno's `entry_json` and `record_json` columns as the anti-example. The fields the new charts need (time to first token, streaming time, token counts) exist on the server only inside `request_history.entry_json` and `chat_turns.turn_json`. *Recommendation:* add typed columns for every field the analytics read, backfill them, and read only typed columns. Moving the rest of those JSON payloads into typed tables is a separate effort and is listed under [Out of scope](#out-of-scope).

**D4. Postman location.** `REPOSITORY_REQUIREMENTS.md` puts the collection under `assets/postman/`; Xeno's is `Xeno.postman_collection.json` at the repository root, and scripts and docs point there. *Recommendation:* leave it where it is.

**D5. Cached prompt tokens.** The token-type chart (C5) wants cached prompt tokens. Chat already records them (`ChatMessageStats.CachedPromptTokens`); request history does not. *Recommendation:* capture `usage.prompt_tokens_details.cached_tokens` (OpenAI shape) and `prompt_eval` cache fields where the relay and listener already parse usage, store them in the new typed columns, and show the cached series only for datasets and buckets that have data, with the legend saying "not recorded" otherwise.

**D6. Desktop and terminal drawing.** The requirements forbid chart libraries in the web dashboards and say nothing about Avalonia or terminal apps. *Recommendation:* no chart libraries anywhere. The desktop and terminal renderers stay hand-built in code and grow the new chart kinds.

**D7. Version numbers.** `VERSIONING.md` forbids version changes without approval. *Recommendation:* no version changes. Changelog entries go under the existing `0.2.0 (alpha, in progress)` heading.

**D8. Cost.** Xeno has no price table; its cost is ledger credits. *Recommendation:* no cost chart in this plan (see [Out of scope](#out-of-scope)).

## Design

### One engine computes every chart

The single rule that keeps four surfaces consistent is that no surface computes analytics. One builder in `Xeno.Core` turns records into a `UsageAnalytics` response: gap-filled buckets, percentiles, top N with Other, heatmap cells, and stage averages. Every endpoint returns that shape, and every surface only draws it. A desktop chart and a web chart of the same data cannot disagree about a p95, because neither of them calculated it.

New files in `src/Xeno.Core/Analytics/` (one class or enum per file, XML docs on every public member, no tuples, no `var`):

| Type | Purpose |
|---|---|
| `UsageDatasetEnum` | `NetworkRequests`, `ServedRequests`, `LocalEndpoint`, `Chat` |
| `UsageRecord` | Normalized projection of one request or turn: `TimestampUtc`, `Model`, `Provider`, `Route`, `ClientId`, `TenantId`, `UserId`, `CredentialId`, `Success`, `StatusCode`, `Fallback`, `TotalMs`, `TimeToFirstTokenMs`, `StreamingMs`, `PromptTokens`, `CachedPromptTokens`, `CompletionTokens`, `Stages` (list of `UsageStageDuration`), `ToolCalls`, `ToolFailures`, `ToolDenied`, `Rating` |
| `UsageStageEnum` | Stable stage codes (see [Stage definitions](#stage-definitions)) |
| `UsageStageDuration` | `Stage`, `Milliseconds` |
| `UsageGroupByEnum` | `Model`, `Provider`, `Route`, `Client`, `Tenant`, `User`, `Credential` |
| `UsageGroupMetricEnum` | `Requests`, `Tokens` |
| `UsageAnalyticsQuery` | Typed query: `FromUtc`, `ToUtc`, `BucketMinutes`, `Model`, `Provider`, `ClientId`, `UserId`, `CredentialId`, `TenantId`, `GroupBy`, `GroupMetric`, `TopN` (default 8, clamped 1 to 20), `UtcOffsetMinutes` (clamped -840 to 840), `Sections` (flags). Setters clamp and null-check |
| `UsageSectionsEnum` | Flags: `Totals`, `Outcome`, `Latency`, `TimeToFirstToken`, `Streaming`, `Throughput`, `Tokens`, `Groups`, `Heatmap`, `Stages`, `Feedback`, `Tools`; default all |
| `UsageDistribution` | `Count`, `Average`, `P50`, `P95`, `P99`, `Maximum` (nullable when `Count` is 0) |
| `UsageBucket` | `BucketStartUtc`, `BucketEndUtc`, `Requests`, `Succeeded`, `Failed`, `Fallbacks` (nullable when the dataset cannot know), `Latency`, `Streaming`, `TimeToFirstToken`, `Throughput` (distributions), `PromptTokens`, `CachedPromptTokens`, `CompletionTokens`, `Groups` (list of `UsageGroupValue`), `Stages` (list of `UsageStageAverage`), `ThumbsUp`, `ThumbsDown`, `ToolCalls`, `ToolFailures`, `ToolDenied` |
| `UsageGroupValue` | `Key`, `Requests`, `Tokens` |
| `UsageGroupTotal` | `Key`, `Requests`, `Failed`, `PromptTokens`, `CompletionTokens`, `Latency` |
| `UsageStageAverage` | `Stage`, `Count`, `AverageMs`, `P95Ms` |
| `UsageHeatmapLayoutEnum` | `HourByMinute`, `DayByHour`, `WeekdayByHour` |
| `UsageHeatmap` | `Layout`, `Rows`, `Columns`, `Cells` (list of `UsageHeatmapCell`), `MaximumTokens` |
| `UsageHeatmapCell` | `Row`, `Column`, `StartUtc`, `Tokens`, `Requests` |
| `UsageTotals` | Totals for the KPI tiles, using the same distributions |
| `UsageAnalytics` | `Dataset`, `FromUtc`, `ToUtc`, `BucketMinutes`, `Totals`, `Buckets`, `GroupBy`, `GroupKeys` (top N in order, then `Other`), `GroupTotals`, `Heatmap`, `StageAverages`, `RecordsScanned`, `Truncated` |
| `UsageAnalyticsBuilder` | `Build(IEnumerable<UsageRecord>, UsageAnalyticsQuery)` and `BuildAsync(IAsyncEnumerable<UsageRecord>, UsageAnalyticsQuery, CancellationToken)` |
| `UsageAnalyticsOptions` | Public properties with defaults: `MaxRecords` (250,000), `DefaultTopN` (8), `PercentileMethod` (nearest rank); passed in rather than hard-coded |

A few rules the builder enforces, each with its own test:

- It emits a bucket for every slot in the range, empty or not. Clients also normalize missing buckets to zero, as `FRONTEND_ARCHITECTURE.md` asks, so both sides gap-fill.
- It computes exact percentiles from stored values, by nearest rank, in one pass that drops each record into its bucket by index. Today's server summary counts each bucket with a separate scan, which is O(records x buckets). Product analytics are computed from stored data; Prometheus quantiles stay in Grafana, as `TELEMETRY_REQUIREMENTS.md` asks.
- Group keys are ranked by the chosen metric over the whole range, and the top N keep their names across every bucket. Everything else becomes `Other`. Empty or null group values become `Unknown`, a stable code the clients localize.
- Throughput is `CompletionTokens / (StreamingMs / 1000)` when streaming time is known and positive, otherwise `CompletionTokens / (TotalMs / 1000)`. Records without completion tokens are left out of throughput, never counted as zero.
- The heatmap uses the viewer's `UtcOffsetMinutes` for hour-of-day and weekday cells. The layout comes from the range: `HourByMinute` for ranges up to 2 hours (6 rows of 10 minutes), `DayByHour` up to 2 days (one row per day, 24 columns), and `WeekdayByHour` beyond (7 rows, 24 columns). A fixed offset ignores a daylight-saving change inside the range; the docs say so.
- When a query would scan more than `MaxRecords`, the builder stops, sets `Truncated`, and every surface shows a notice saying the range was too large to read in full. It never samples silently.

### Stage definitions

Stages are derived from timings Xeno already records, and every stage has a stable code that the clients translate. A stage is left out of a record when its inputs are missing; it is never guessed. Phase 0 checks each formula against real timelines before code depends on it, because the telemetry fields were written for display, not for subtraction.

| Dataset | Stage code | Display label (en) | Formula |
|---|---|---|---|
| Network requests | `ServerOverhead` | Xeno server | `TotalTimeMs - ClientRoundTripMs` |
| Network requests | `RelayAndClient` | Relay and client | `ClientRoundTripMs - UpstreamTotalMs` |
| Network requests | `ProviderHeaders` | Provider to headers | `RequestToHeadersMs` |
| Network requests | `ProviderFirstToken` | Headers to first token | `TimeToFirstTokenMs - RequestToHeadersMs` |
| Network requests | `Streaming` | Streaming | `TotalStreamingTimeMs` |
| Served requests | `ProviderHeaders` | Provider to headers | `TimeToHeadersMs` |
| Served requests | `ProviderFirstToken` | Headers to first byte | `TimeToFirstByteMs - TimeToHeadersMs` |
| Served requests | `Streaming` | Streaming | `DurationMs - TimeToFirstByteMs` |
| Local endpoint | `Queue` | Waiting for a slot | `QueuedMs` |
| Local endpoint | `ProviderFirstToken` | Provider to first byte | `FirstByteMs - QueuedMs` |
| Local endpoint | `Streaming` | Streaming | `LatencyMs - FirstByteMs` |
| Chat | `ModelFirstToken` | Model to first token | `TimeToFirstTokenMs` |
| Chat | `ModelStreaming` | Model writing | sum of `ModelMs - TimeToFirstTokenMs` over the turn's model calls |
| Chat | `Tools` | Tools | `ToolMs` |
| Chat | `Approval` | Waiting for approval | time tool calls spent `Pending` (new field, Phase 2) |
| Chat | `Other` | Other | `TotalMs` minus the stages above, when positive |

Negative results (clock skew, or a field measured from a different start) are clamped to zero and counted in a `StageAnomalies` counter on the response, so a bad formula shows up in testing instead of in a chart.

### Storage changes

Every change is additive, versioned, idempotent, and implemented for every provider the store supports, with handwritten SQL per provider as `BACKEND_ARCHITECTURE.md` requires.

**Server `request_history` (SQLite, PostgreSQL, MySQL, SQL Server).** New nullable typed columns: `time_to_first_token_ms`, `streaming_ms`, `client_round_trip_ms`, `upstream_ms`, `request_to_headers_ms`, `prompt_tokens`, `cached_prompt_tokens`, `completion_tokens`, `response_bytes`, `fallback`. New indexes: `(tenant_id, selected_client_id, created_utc)` and `(tenant_id, user_id, created_utc)`. A migration backfills the columns from `entry_json` in batches of 5,000 rows, tracked in `schema_migrations` so it resumes after a restart. The write path fills them from `RequestHistoryTelemetry`. Files: `src/Xeno.Core/Database/{Sqlite,Postgresql,Mysql,SqlServer}/*RequestHistoryMethods.cs` and `IRequestHistoryMethods.cs`, which gains `EnumerateUsageAsync(RequestHistoryUsageFilter, CancellationToken)`. That method returns `IAsyncEnumerable<UsageRecord>` and reads only typed columns.

**Server `chat_turns` (`DbChatMethods`, all four databases).** New typed columns: `time_to_first_token_ms`, `total_ms`, `tool_ms`, `tool_call_count`, `tool_failures`, `tool_denied`, `model_calls`, `prompt_tokens`, `cached_prompt_tokens`, `completion_tokens`, `approval_ms`, backfilled from `turn_json`, plus `EnumerateUsageAsync`.

**Local chat store (`SqliteChatStore`).** The same columns where they are missing, with a migration in the existing migration list, plus `EnumerateUsageAsync`.

**Local endpoint records.** A new `local_inference_requests` table in the local dashboard database, with typed columns matching `LocalInferenceRequestRecord` plus cached prompt tokens. The listener runtime writes each record there, as well as to the existing JSON log (other processes still read the log). Retention is a new setting, `LocalInferenceServer.RequestRetentionDays` (default 30, 1 to 365), pruned hourly like request history. Without this table the Local endpoint dataset can never show more than its last 1000 requests, so a "last month" chart would be quietly wrong.

**Chat approval time.** `ChatToolCallRecord` gains `ApprovalMs`, set by `ChatEngine` when a pending call is answered or times out. `ChatTurn` gains `ApprovalMs`.

### Endpoints

All routes are read-only, return `UsageAnalytics` (or `ProviderHealthStrip`), take typed query DTOs, and return 400 with a clear message for a bad range or bucket size, never 500. A range that would need more than 2,000 buckets is a 400. When request history capture is turned off, the request-history routes return an empty `UsageAnalytics`, not a 404, matching `BACKEND_ARCHITECTURE.md` ("Opting Out").

| Surface | Method and path | Dataset | Authorization |
|---|---|---|---|
| Server | `GET /v1.0/api/request-history/analytics/usage` | Network requests | Request history read. Scope follows the existing request history rules: users see their own requests, tenant admins their tenant, global admins any tenant through `tenantId`. `groupBy=User` or `Credential` needs tenant admin; `groupBy=Tenant` needs global admin |
| Server | `GET /v1.0/api/chat/analytics/usage` | Chat | `Chat` permission; `scope=tenant` for tenant admins, as on the existing chat analytics route |
| Server | `GET /v1.0/api/client-telemetry/providers/health` | Provider telemetry | Client telemetry read, tenant-scoped |
| Local | `GET /api/v1/requests/analytics/usage` | Served requests | Local token |
| Local | `GET /api/v1/local-inference/analytics/usage` | Local endpoint | Local token |
| Local | `GET /api/v1/chat/analytics/usage` | Chat | Local token |
| Local | `GET /api/v1/telemetry/providers/health` | Provider telemetry | Local token |

Query parameters, shared by every usage route: `fromUtc`, `toUtc`, and `bucketMinutes` (all required, as summary routes are in `BACKEND_ARCHITECTURE.md`), then optional `model`, `provider`, `clientId`, `userId`, `credentialId`, `tenantId` (server, global admin only), `groupBy`, `groupMetric`, `topN`, `utcOffsetMinutes`, and `sections` (comma-separated `UsageSectionsEnum` names parsed into flags; unknown names are a 400). Health routes take `fromUtc`, `toUtc`, `bucketMinutes`, optional `providerName`, and on the server `clientId`.

`ProviderHealthStrip` (new, `src/Xeno.Core/Analytics/`) has `Providers` (list of `ProviderHealthRow`: `ProviderName`, `ClientId`, `UptimePercent`, `ConsecutiveFailures`, `Cells`), and `ProviderHealthCell` has `StartUtc`, `Status` (`ProviderHealthStatusEnum`: `Healthy`, `Degraded`, `Unhealthy`, `NoData`), `Samples`, `Failures`. A bucket is `Healthy` when every sample is healthy, `Unhealthy` when every sample failed, `Degraded` otherwise.

Server routes register in `src/Xeno.Server/Routes/RequestHistoryRoutes.cs`, `ChatRoutes.cs`, and the client telemetry registrar, with `OpenApiRouteMetadata` on each. Each is added to the path registry in `XenoServer.cs` (the `/openapi.json` operations table) and to the central operation-to-permission map. Local routes go in `LocalDashboardService.cs` (requests and telemetry) and `LocalDashboardService.Chat.cs`.

The existing summary and chat analytics routes stay as they are, so nothing that calls them breaks. The server's `SummarizeRequestHistoryAsync` returns anonymous objects today; it is rewritten to return a named DTO and to use the single-pass bucketing, without changing its JSON.

### Settings

New settings go through the settings catalogs, so every Settings editor (desktop, terminal, local web, server web) shows them with their "applies now" or "restart required" note:

- Server `Analytics.MaxRecordsPerQuery` (default 250,000, clamped 10,000 to 5,000,000; applies now) and `Analytics.DefaultTopN` (default 8, clamped 1 to 20; applies now).
- Client `Analytics.MaxRecordsPerQuery` and `Analytics.DefaultTopN` with the same defaults, backfilled into existing `client.json` files.
- Client `LocalInferenceServer.RequestRetentionDays` (default 30, clamped 1 to 365; applies now).

### Instrumentation

`TELEMETRY_REQUIREMENTS.md` requires every new code path to be instrumented, best effort, with low-cardinality labels:

- Histogram `xeno_analytics_query_duration_seconds`, labels `dataset` (5 values) and `outcome` (`ok`, `truncated`, `error`).
- Counter `xeno_analytics_records_scanned_total`, label `dataset`.
- Span `analytics usage` with attributes `xeno.analytics.dataset`, `xeno.analytics.records`, and `xeno.analytics.truncated`.

No ids, model names, or user values go into labels; per-client and per-user splits come from the database. The client gets the same meters when its telemetry export is on. `OBSERVABILITY.md` documents them. If the Grafana assets add dashboards later, these join the Xeno folder.

### One chart contract for every surface

Today `XenoChart` describes stacked columns only, and `XenoCharts` builds the desktop and terminal versions of five charts. Phase 4 grows it into the single definition of every chart on every surface.

- `XenoChartKindEnum` (new): `StackedColumns`, `Lines`, `HorizontalBars`, `Heatmap`, `StatusStrip`. `XenoChart` gains `Kind`, `Id` (the catalog id, for example `usage.latency`), `Cells` (heatmap and strip cells), `Bars` (labelled values for horizontal bars), and `BucketRanges` (start and end per column, for drill-down).
- `XenoUsageCharts` (new, `src/Xeno.Client.Core/Presentation/`) builds C1 to C13 from `UsageAnalytics` and `ProviderHealthStrip`, and V1 from a request or turn. Desktop and terminal pages call only these builders.
- `XenoChartCatalog` (new) lists every chart: id, kind, i18n title key, unit, and its series with key, tone, and i18n label key. A test serializes it to JSON and compares the result with the committed `dashboard/src/charts/chartCatalog.json`. The web charts read their series, colors, and order from that file. If the C# side gains a series and nobody updates the web, the test fails.
- Tones map to the shared semantic colors on every surface: success green, danger red, warning amber, info blue, plus an ordered categorical palette for group series (8 colors plus a neutral for `Other`) that passes contrast in light and dark. Status never relies on color alone: legends and tooltips name it, the health strip uses a different glyph or pattern per status, and the terminal uses different characters as well as colors.

**Desktop** (`src/Xeno.Desktop/Controls/`). `ColumnChart` becomes `XenoChartView`, which draws every kind. Tooltips show exact values; clicking a column raises `BucketSelected` with its start and end. Keyboard focus moves across columns with the arrow keys, and Enter selects.

**Terminal** (`src/Xeno.Tui/Shell/ChartView.cs`). The view draws every kind in character cells:

- Lines plot one glyph per column per series (`●` p50, `○` p95, `◆` p99, `×` maximum), with a legend row.
- Horizontal bars use eighth-block characters and a value label.
- The heatmap shades with ` ░▒▓█` in the accent color.
- The strip uses `█` healthy, `▒` degraded, `×` unhealthy, and `·` no data.

A cursor column, moved with the left and right arrow keys or a mouse click, shows that bucket's values on the message line, and Enter or a double click drills down. Mouse handling follows `TuiTestSuites` (`TuiHarness.Click`), as `AGENTS.md` requires for every terminal control.

**Web** (`dashboard/src/charts/`, shared by the local and server dashboards). Hand-built SVG components, with no chart library, as `FRONTEND_ARCHITECTURE.md` requires:

- `StackedColumnChart.jsx` generalizes today's `ActivityChart` and `ChatActivityChart`, which are migrated onto it.
- `LineChart.jsx`, `HorizontalBarChart.jsx`, `HeatmapChart.jsx`, `StatusStrip.jsx`.
- `ChartTooltip.jsx` renders through `createPortal` so cards cannot clip it.
- `ChartCard.jsx` holds the title, legend, range control, refresh button, last-loaded time, and loading, empty, and error states (with retry).

Axes use about 3 Y ticks and about 8 X labels. Every axis label and tooltip timestamp goes through the locale formatters in `dashboard/src/i18n/formatters.js`. The usage pages talk to the backend through a `UsageApiContext`, the same pattern as `ChatApiContext`, so local and server share one page.

### The Usage page on each surface

| | Local web | Server web | Desktop | Terminal |
|---|---|---|---|---|
| Where | New nav item Usage | Analytics page becomes Usage (route `/dashboard/usage`; `/dashboard/analytics` redirects) | New page `XenoPageEnum.Usage` | New page `XenoPageEnum.Usage` |
| Datasets | Served requests, Local endpoint, Chat | Network requests, Chat | Served requests, Local endpoint, Chat | Served requests, Local endpoint, Chat |
| Filters | Range, dataset, model, provider, group by, group metric | Range, dataset, model, client, user (admins), tenant (global admins), group by, group metric | Same as local web | Same as local web, as dropdowns |
| Layout | KPI tiles, then C1 to C12 as cards, two per row on wide screens and one per row on narrow | Same | Scrollable two-column grid of chart cards | Filters row, KPI block, one chart at a time with a Chart dropdown that cycles C1 to C12, and the totals-by-group table below |
| Drill down | Opens Requests, the Local endpoint request list, or Chat history with `fromUtc` and `toUtc` (and model when the series is a model) | Opens Request History or Chat review the same way | Same, by page navigation with a filter | Same |

The page is added once in `XenoNavigationCatalog` and `XenoMenuLayout` (and in the local and server web navigation) and rendered in every app, as `AGENTS.md` requires. The terminal layout must fit `XenoTuiApp.MinimumWidth` x `MinimumHeight` (120 x 36). Charts that do not apply to the chosen dataset (feedback and tool calls outside Chat) are hidden, not shown empty. The empty state says why: no traffic retained, nothing matched the filters, or the backend could not be reached, as `DASHBOARD_STYLE_AND_USABILITY.md` asks.

The provider health strip (C13) goes on the Provider telemetry page on every surface and on each server client's telemetry view. Request timing bars (V1) go in the request detail views (local Requests, Local endpoint, server Request History) and the chat turn detail views on every surface; the terminal shows them as a fenced text block built with `XenoMarkdown`.

### CLI and MCP

- `xeno usage [--dataset served|endpoint|chat] [--range hour|day|week|month] [--group-by model|provider|route] [--model <name>] [--json]` prints the KPI block, latency and time-to-first-token percentiles, tokens by type, totals by group, and average time by stage, reading the local API like `xeno chat`. `--json` prints the `UsageAnalytics` response. Errors go to standard error with a non-zero exit code.
- A read-only MCP tool, `xeno_usage`, takes the same arguments and returns the same JSON, registered in `XenoToolCatalog` and documented in `MCP.md`. Chat conversations with tools on can then answer "how slow was llama3.1 this week" from real numbers.

### SDKs

The three SDKs cover the server API, so each gains the three server routes, with typed models, tests, README sections, and console examples. Local routes stay out of the SDKs, as today.

| SDK | Methods | Models | Tests |
|---|---|---|---|
| C# `src/sdk/csharp/Xeno.Sdk` | `GetUsageAnalyticsAsync(UsageAnalyticsQuery, CancellationToken)`, `GetChatUsageAnalyticsAsync(...)`, `GetProviderHealthAsync(ProviderHealthQuery, CancellationToken)` | Mirror the `Xeno.Core.Analytics` DTOs in the SDK's own model folder | `Xeno.Sdk.Tests` against a stub server on `127.0.0.1` |
| JavaScript `src/sdk/javascript` | `getUsageAnalytics(query)`, `getChatUsageAnalytics(query)`, `getProviderHealth(query)` | JSDoc typedefs | `test/` |
| Python `src/sdk/python` | `get_usage_analytics(...)`, `get_chat_usage_analytics(...)`, `get_provider_health(...)` | `XenoModel` subclasses | `tests/` |

The SDK package versions do not change (D7).

## Route inventory

`DASHBOARD_STYLE_AND_USABILITY.md` asks for this table before coding.

| Route | User job | Roles | Backend resources | Table and filter needs | Actions and modals | Empty and error states |
|---|---|---|---|---|---|---|
| Local `usage` page | See how this machine's served, local endpoint, and chat traffic performs | Local user | `/api/v1/requests/analytics/usage`, `/api/v1/local-inference/analytics/usage`, `/api/v1/chat/analytics/usage` | Dataset, range, model, provider, group by, group metric; totals-by-group table (shared DataTable) | Drill down to Requests, Local endpoint, or Chat history; refresh | No traffic retained; nothing matched; client not running; range truncated |
| Local Provider telemetry page (existing) | Spot unhealthy providers | Local user | `/api/v1/telemetry/providers/health` plus existing rollups | Range, provider | Click a cell to open that provider's samples for the bucket | No samples yet; telemetry off |
| Local request and chat detail (existing) | See where one request's time went | Local user | Existing detail routes | None | None | Stage data not recorded for this request |
| Server `/dashboard/usage` | See tenant traffic, latency, tokens, and who uses what | Tenant member (own requests), tenant admin (tenant), global admin (any tenant) | `/v1.0/api/request-history/analytics/usage`, `/v1.0/api/chat/analytics/usage` | Dataset, range, model, client, user and tenant by role, group by, group metric; totals-by-group table | Drill down to Request History or Chat review; refresh | No requests retained; capture off; nothing matched; not authorized for this grouping (403 message) |
| Server client telemetry view (existing) | Check a client's providers | Tenant admin, global admin | `/v1.0/api/client-telemetry/providers/health` | Client, provider, range | Click a cell to open samples | No samples; client never reported |
| Server Request History detail (existing) | See where one request's time went | As today | Existing detail route | None | None | Stage data not recorded |

## Phases

Each phase ends with a full build without warnings, all three .NET harnesses green (`Test.Automated`, `Test.Xunit`, `Test.Nunit`), and the dashboard suites green once the phase touches the dashboard. No phase is done with a failing or skipped test it introduced.

### Phase 0: Settle and verify

- [ ] Get answers to D1 to D8.
- [ ] Check every stage formula against 20 real timelines from each dataset (server, served, listener, chat). Correct the table above where a field turns out to measure from a different start, and record the findings in the plan.
- [ ] Confirm where cached prompt tokens appear in Ollama, vLLM, and OpenAI-compatible usage payloads and which of them the relay and listener already parse (D5).
- [ ] Measure the current server summary on 100,000 synthetic rows in each database to set the performance budget used by the Phase 3 test.

Done when the decisions are written into this plan and the stage table has been checked.

### Phase 1: Analytics engine

- [ ] Add the `src/Xeno.Core/Analytics/` types and `UsageAnalyticsBuilder` as specified.
- [ ] Add `UsageRecord` projections: `RequestHistoryEntry`, `LocalRequestHistoryEntry`, `LocalInferenceRequestRecord`, and `ChatTurn` to `UsageRecord`, each in its own mapper class.
- [ ] Apply D1 in `HistoryRanges` (C#) and `chatRanges.js` (web).
- [ ] Tests in a new `src/Test.Shared/UsageAnalyticsTestSuites.cs`, registered in `SharedTestSuites.All`:
  - gap filling for every preset;
  - exact percentiles against hand-computed values, including one record, two records, and ties;
  - top N with `Other` and `Unknown`;
  - each heatmap layout, with offsets of -480, 0, and 330 minutes;
  - each stage formula, with missing inputs and negative results;
  - throughput with and without streaming time;
  - fallback being null where the dataset cannot know it;
  - truncation at `MaxRecords`;
  - an empty dataset;
  - a cancelled token.

Done when the builder passes on all three harnesses and nothing outside the tests calls it yet.

### Phase 2: Storage

- [ ] Add the server `request_history` typed columns, indexes, batched backfill, and write path, for SQLite, PostgreSQL, MySQL, and SQL Server.
- [ ] Add `IRequestHistoryMethods.EnumerateUsageAsync` for each provider, reading only typed columns, scoped by tenant from the caller's context.
- [ ] Add the server `chat_turns` typed columns, backfill, and `EnumerateUsageAsync` in `DbChatMethods`.
- [ ] Add the local chat store columns and migration, and `EnumerateUsageAsync`.
- [ ] Add the `local_inference_requests` table, writes from `LocalInferenceServerRuntime`, hourly pruning, and the `RequestRetentionDays` setting (catalog, backfill, every Settings editor).
- [ ] Add `ChatToolCallRecord.ApprovalMs` and `ChatTurn.ApprovalMs`, recorded by `ChatEngine`.
- [ ] Capture cached prompt tokens per D5.
- [ ] Tests:
  - provider-matrix cases, run with `--db-provider` for each database, for the new columns, the backfill (including resuming after an interrupted batch), tenant isolation (no cross-tenant rows in any grouping), and user scoping;
  - local migration from a database created before this change;
  - listener records reaching the table and being pruned;
  - approval time on allowed, denied, and timed-out calls.

Done when an upgraded database of each kind answers `EnumerateUsageAsync` with correct typed values.

### Phase 3: Endpoints, settings, instrumentation

- [ ] Add the seven routes listed above, with typed query DTOs, validation (400 for bad input), authorization (central map), OpenAPI metadata, and path registry entries.
- [ ] Rewrite `SummarizeRequestHistoryAsync` with a named DTO and single-pass bucketing, keeping its JSON byte for byte (a test compares old and new output).
- [ ] Add the `ProviderHealthStrip` builder over provider telemetry samples, local and server.
- [ ] Add the `Analytics` settings (server and client), through every settings catalog and editor.
- [ ] Add the meters and spans.
- [ ] Tests:
  - route cases for every route and dataset;
  - a 403 for `groupBy=User` as a tenant member and for `tenantId` as a tenant admin;
  - a 400 for each invalid parameter;
  - empty results with request history capture off;
  - OpenAPI expectations (`OpenApiRouteExpectation`) for the server routes;
  - local API cases through `ChatApiFixture` and the local dashboard harness;
  - a performance budget case: 100,000 records analyzed within the Phase 0 budget on SQLite in the automated run.

Done when every route returns correct `UsageAnalytics` for seeded data on every database, and the existing summary output is unchanged.

### Phase 4: Chart contract and renderers

- [ ] Add `XenoChartKindEnum` and extend `XenoChart` as specified.
- [ ] Add `XenoUsageCharts` (C1 to C13, V1) and `XenoChartCatalog`, plus the committed `dashboard/src/charts/chartCatalog.json`.
- [ ] Rework the five existing `XenoCharts` builders onto the catalog, with no visible change beyond D1.
- [ ] Turn desktop `ColumnChart` into `XenoChartView` (every kind, tooltips, click and keyboard bucket selection).
- [ ] Extend the terminal `ChartView` with every kind, a cursor, mouse and keyboard drill-down, and the glyph legends.
- [ ] Tests:
  - builder cases for every chart;
  - the catalog JSON matching the committed file;
  - `TuiHarness` rendering of every kind at 120 x 36, with mouse and keyboard bucket selection;
  - `FrontEndTestSuites` cases that the desktop view accepts every kind headlessly.

Done when the desktop and terminal can draw every chart from seeded `UsageAnalytics`.

### Phase 5: Web dashboards

- [ ] Add the `dashboard/src/charts/` components and migrate `ActivityChart` and `ChatActivityChart` onto `StackedColumnChart`.
- [ ] Add the shared `dashboard/src/usage/UsagePage.jsx`, `UsageApiContext`, and local and server API adapters.
- [ ] Add the local Usage nav item and the server Usage route (with the `/dashboard/analytics` redirect).
- [ ] Add the health strip to the local Provider telemetry page and the server client telemetry view.
- [ ] Add timing bars to the local and server request detail and chat turn detail modals.
- [ ] Add drill-down: Requests, Local endpoint, Chat history, server Request History, and Chat review accept `fromUtc`, `toUtc`, and `model` from the URL or navigation state and apply them to their filters. Refresh keeps active table filters.
- [ ] Add i18n keys for every new string in English and Spanish, with every locale passing `npm test`: titles, legends, stage, dataset, group, token type and status display labels, empty states, the truncation notice, and tooltips. Enum values come from the display-label layer, never raw.
- [ ] Tests (Playwright, local and server suites):
  - each chart populated, empty, loading, and error (mocked);
  - drill-down navigation and the filter it applies;
  - the tooltip escaping a clipped card;
  - range and refresh;
  - the dataset picker hiding chat-only charts;
  - the 403 message for a grouping the role cannot use;
  - visual QA at 1280, 768, and 390 pixels in light and dark, kept as test screenshots;
  - a pseudo-locale run (expanded text) to check that axis labels and legends do not overflow.

Done when both dashboards show every chart and both suites pass.

### Phase 6: Desktop and terminal pages

- [ ] Add the Usage page to `Xeno.Desktop` and `Xeno.Tui`, through the navigation catalog and menu layout.
- [ ] Add the health strip to both Provider telemetry pages.
- [ ] Add timing bars to the request and chat turn detail views in both apps.
- [ ] Add drill-down from every time chart to the matching page with the bucket's filter applied.
- [ ] Add `XenoWorkspace` methods that fetch usage analytics and health, so the pages never talk to storage directly.
- [ ] Tests: `TuiTestSuites` cases for the Usage page (every chart through the Chart dropdown, mouse and keyboard, drill-down landing on a filtered page, the minimum size), and `FrontEndTestSuites` cases for the navigation entries and the workspace methods.

Done when both apps show every chart, and every chart matches the web output for the same seeded data (a shared test seeds one dataset and compares the `XenoChart` series with the web catalog series).

### Phase 7: CLI and MCP

- [ ] Add `xeno usage` with the options above, in the CLI help, and in the README command list.
- [ ] Add the `xeno_usage` MCP tool to `XenoToolCatalog`, read-only, so chat tools can use it.
- [ ] Tests: CLI cases (each dataset, `--json`, a bad option exiting non-zero with the message on standard error, the client not running) and MCP cases (tool listing, arguments, result shape).

Done when the CLI and MCP return the same numbers as the Usage page for seeded data.

### Phase 8: SDKs

- [ ] Add the C#, JavaScript, and Python methods and models above.
- [ ] Add tests in each SDK's harness against a stub server on `127.0.0.1`, covering every method, every query parameter, error mapping (400, 403), and an empty response.
- [ ] Update each SDK README and console example.

Done when all three SDK test suites pass in CI.

### Phase 9: Postman, API explorers, documentation

- [ ] Postman (`Xeno.postman_collection.json`): add the seven requests in their resource folders, with request-level descriptions, every query parameter documented, `{{baseUrl}}` and token variables, and saved example responses. Check that every new description is free of em-dashes.
- [ ] Add API explorer samples for the new routes in the local dashboard and the server dashboard.
- [ ] Update `REST_API.md`: every new route with method, path, parameters, response shape, status codes, authorization, and an example.
- [ ] Write a new `USAGE_ANALYTICS.md`: what each chart shows and how to read it, the datasets, stage definitions, heatmap layouts and the time-zone note, percentile method, truncation, drill-down on each surface, and settings.
- [ ] Update `DESKTOP_AND_TERMINAL.md` (Usage page, chart keys and mouse use), `CHAT.md` (chat usage analytics and approval time), `HYBRID_MODELS.md` (fallback series), `OBSERVABILITY.md` (new meters and spans, and the difference between product analytics and Prometheus), and `MCP.md` (`xeno_usage`).
- [ ] Update `README.md` (feature list, `xeno usage`, link to `USAGE_ANALYTICS.md`, refreshed screenshots in `assets/` if any show the old Analytics page) and `DOCKERHUB_README.md` to match.
- [ ] Add `CHANGELOG.md` entries under `0.2.0 (alpha, in progress)`: new charts and pages, new routes, new settings, the bucket preset change (D1, marked as a behavior change), storage migrations and backfill, the CLI command, the MCP tool, and the SDK methods.
- [ ] Add a rule to `AGENTS.md`: usage charts come from `UsageAnalyticsBuilder` and `XenoChartCatalog`; a chart is added in both before any surface draws it.
- [ ] Update the Xeno column and the gaps table in `~/Code/REPORTING_MATRIX.md`.

Done when every README, `REST_API.md`, and Postman example runs as written against a local server and client.

### Phase 10: Bring the existing charts into line

The 2026-11-10 review found existing charts that differ between surfaces. Leaving them would undercut the rule this plan is built on, so they are fixed here:

- [ ] Provider telemetry: the same metric picker, provider and model filters, and minimum and maximum in tooltips on all four surfaces. Today only the local web dashboard has all three, and the server shows three fixed metrics.
- [ ] Host telemetry: chart all seven collected metrics on all four surfaces, behind a metric picker, instead of CPU, memory, and GPU only.
- [ ] Refresh: manual refresh on every chart everywhere, and the same auto-refresh choices (off, 15 seconds, 1 minute) on desktop, terminal, and web. Pause while a modal is open.
- [ ] Default range: last day on every surface and page.
- [ ] Connected clients: record the count on the server (a typed `connected_client_samples` table, sampled each minute, 30-day retention) and chart it from there, instead of from browser storage. Add it to the server Usage page.
- [ ] Tests for each, on every surface they touch.

Done when the same seeded data draws the same chart on every surface that shows it.

### Phase 11: Release verification

- [ ] Run the full build without warnings and all three .NET harnesses, including the provider-matrix run against PostgreSQL, MySQL, and SQL Server in CI.
- [ ] Run all three SDK suites and both dashboard suites, plus `npm test` for i18n.
- [ ] Rebuild the dashboard and server Docker images and restart the running containers, as `DASHBOARD_STYLE_AND_USABILITY.md` requires whenever dashboard code changes. Publishing images stays a separate, explicitly approved step.
- [ ] Run a simulated user testing session per `SIMULATED_USER_TESTING.md` on a clean `xeno-usertest` instance on `127.0.0.1`. Cover every surface (both web dashboards, desktop, terminal, CLI, MCP, REST, all three SDKs) and include the cross-surface journey "the same data shows the same values, units, rounding, and time zone everywhere". File the report at `user-testing/<yyyy-mm-dd>-reporting.md`. Fix nothing from it until the findings have been reviewed.
- [ ] Write the handoff: what shipped, missing backend capabilities, visual QA done, tests run, and open findings.

## Testing summary

| Layer | Where | What |
|---|---|---|
| Engine | `Test.Shared/UsageAnalyticsTestSuites.cs` | Buckets, percentiles, grouping, heatmap, stages, throughput, truncation, cancellation |
| Storage | `Test.Shared` provider-matrix cases (`--db-provider`) | Columns, backfill and resume, tenant isolation, user scope, local migrations, listener table |
| Routes | `Test.Shared` server and local API cases | Every route, validation, authorization, capture off, OpenAPI, the summary staying unchanged, the performance budget |
| Presentation | `Test.Shared` (`FrontEndTestSuites`, `TuiTestSuites`) | Chart builders, catalog sync, every renderer kind, Usage pages, mouse and keyboard drill-down, minimum terminal size |
| CLI and MCP | `Test.Shared` | `xeno usage`, `xeno_usage` |
| Web | `dashboard/tests` Playwright (local and server), `npm test` | Every chart state, drill-down, tooltips, roles, visual QA at three widths and two themes, pseudo-locale |
| SDKs | `src/sdk/*` harnesses | Every new method |
| End to end | Simulated user testing session | Every surface, cross-surface agreement |

All of these run in CI on every push, with no tag or filter that drops cases, per `BACKEND_TEST_ARCHITECTURE.md`.

## Risks

**Large ranges on large tenants.** A month of a busy tenant can be millions of rows. The typed columns and composite indexes keep reads narrow, `MaxRecordsPerQuery` caps the work, and the truncation flag tells the user instead of drawing a partial month as if it were whole. If Phase 0 shows the budget cannot be met, the fallback is an hourly rollup table with typed columns, which this plan deliberately does not build up front.

**Backfill on upgrade.** Backfilling `entry_json` into typed columns on a large server database takes time. It runs in batches in the background, resumes after a restart, and the usage routes report `Truncated` with a "backfill in progress" reason until it finishes.

**Stage formulas.** Derived stages are only as good as the timestamps behind them. Phase 0 checks them, and the anomaly counter keeps a bad formula visible.

**Terminal legibility.** Four percentile series in a 118-column chart can crowd each other. The glyph order puts p50 on top, and the cursor readout gives exact values, so nothing depends on reading overlapping glyphs.

## Out of scope

- Cost charts (D8). Ledger credits by model would be the Xeno-shaped version, and it deserves its own plan.
- Moving `entry_json`, `turn_json`, and `message_json` into fully typed tables beyond the analytics columns (D3).
- Three-month and twelve-month ranges, a lines or bars toggle, and copying charts as images (catalog R22 and R23). The routes already accept any range, so these become drawing work later.
- Rate-limit and quota charts (R11). Xeno has no such data.
- Changing any version number (D7).

## Definition of done

Every chart in [What gets built](#what-gets-built) appears on the local web dashboard, the server web dashboard, the desktop app, and the terminal app. Each one is computed by `UsageAnalyticsBuilder`, described once in `XenoChartCatalog`, and drills down where it has time buckets. The routes are in the OpenAPI document, the API explorers, Postman, and `REST_API.md`, and the three SDKs call them. `xeno usage` and `xeno_usage` return the same numbers as the charts. The existing charts no longer differ between surfaces. Every test layer above passes in CI, the simulated user testing report has been reviewed, and `REPORTING_MATRIX.md` shows Xeno with no planned gaps left.

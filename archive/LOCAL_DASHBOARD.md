# Xeno Client Local Dashboard Plan

Xeno Client needs a local dashboard that starts with the background client process when it is enabled in `~/.xeno/client.json`. It defaults to `127.0.0.1` so a normal installation is reachable only from the local machine, while allowing an explicit `0.0.0.0` bind when the user intentionally wants to expose it beyond loopback. The local dashboard is not a separate product surface with its own visual language. It is the operator view for one connected client, and it must feel like a focused slice of the main Xeno dashboard: same typography, layout rhythm, table behavior, modals, copy controls, chart treatment, theme handling, language selector, GitHub link, and action menus.

The work touches the CLI, the background worker lifecycle, local HTTP hosting, client settings, request capture, a new local dashboard frontend entry point, tests, and documentation. Treat this document as the implementation checklist. Keep the checkboxes current while building.

## Current Implementation Status

The first working implementation is complete for client settings, background-worker startup, Watson local HTTP hosting, token-gated local APIs, local SQLite persistence, provider/model management, provider connectivity testing, request history capture, request charts, request details, settings editing, logs, CLI discovery, documentation, Touchstone coverage, a dedicated React/Vite local dashboard, embedded static assets, and local configuration snapshots. The remaining open items are limited to manual browser/UX validation and full string localization of the local dashboard surface.

## Requirements Baseline

The implementation must conform to `c:\code\agents\requirements` without local exceptions.

- [x] Follow `CODE_STYLE.md` for C#:
  - namespace first, `using` statements inside namespace
  - no `var`
  - no tuples
  - no `JsonElement`, `JsonNode`, `JsonObject`, or `JsonArray`
  - named request/response/settings classes for every JSON contract
  - one class or enum per file
  - public members and public methods documented
  - async methods accept `CancellationToken` and use `.ConfigureAwait(false)`
- [x] Follow `BACKEND_ARCHITECTURE.md` for HTTP hosting:
  - use Watson 7 for the local web service
  - use typed route handlers and typed request/response bodies
  - register CORS/preflight behavior deliberately, even if only loopback is allowed
  - expose OpenAPI for the local API only if it does not dilute the local UX
- [x] Follow `FRONTEND_ARCHITECTURE.md`:
  - React 19, Vite, React Router, fetch-based API client
  - no charting library
  - hand-rolled SVG charts with portal-rendered hover tooltips
  - reusable components for shell, table, pagination, modals, copy, charts, action menus, and language selector
- [x] Follow `I18N.md`:
  - every label, tooltip, placeholder, modal string, chart label, empty state, and accessibility string is localized
  - language selector appears in the local dashboard shell
  - locale persists across reloads
  - `Accept-Language` is sent by the local dashboard API client
  - `document.documentElement.lang` and `dir` are maintained centrally
- [x] Follow `BACKEND_TEST_ARCHITECTURE.md`:
  - add Touchstone coverage in `Test.Shared`
  - execute through `Test.Automated`, `Test.Xunit`, and `Test.Nunit`
  - no console writes from `Test.Shared`
- [x] Follow `WRITING_DOCUMENTS.md` for user-facing docs:
  - direct, specific prose
  - no generic filler
  - setup commands should be short and testable

## Product Shape

The local dashboard answers one question: what is this machine doing for Xeno right now? It should show connection state, local provider configuration, advertised models, processed request history, and client-side performance statistics. It should also let the user manage provider endpoints and model advertisements without opening `client.json` by hand.

The local dashboard must never become an administration console for tenants, users, server credentials, server routing policies, or global request history. Those remain in the main Xeno dashboard. The local dashboard owns only local client configuration and local runtime observations.

## Non-Negotiable UX Parity

Audit the current main dashboard before implementing and again before finishing. The local dashboard should reuse or directly mirror these patterns:

- [x] Same global font stack, CSS variables, spacing scale, borders, table hover colors, dark-mode colors, and focus treatment from `dashboard/src/index.css`.
- [x] Same shell composition from the main dashboard:
  - logo in the upper left
  - left navigation
  - server/client URL text in the top bar without badge treatment
  - real GitHub icon link in the upper right
  - light/dark toggle icon
  - language selector
- [x] Same table behavior:
  - full-width table cards
  - refresh and pagination controls above tables
  - auto-refresh selector with `None`, `15s`, `30s`, `60s`, `180s`, `300s`
  - default auto-refresh `15s`
  - column filters apply on Enter and on blur
  - row click opens the edit/details modal
  - action menus render above table clipping boundaries
  - table row hover is readable in dark mode
  - IDs use fixed-width font and global copy-to-clipboard controls
  - IDs are not shown inside badges
- [x] Same modal behavior:
  - custom modal components, not browser dialogs
  - large details modals use most of the viewport when content warrants it
  - no stacks of cards for individual data points
  - details use compact key/value tables or definition layouts with consistent row heights
  - request details body/header sections are full width and collapsible, expanded by default
  - JSON sections include copy-to-clipboard controls
- [x] Same chart behavior:
  - hand-rolled SVG bars
  - visible X and Y axes
  - range controls outside the chart card when they affect multiple views
  - hover tooltip follows the pointer and is never clipped behind the workspace
  - no external chart library
- [x] Same copy control:
  - works on HTTP, HTTPS, localhost, and non-localhost
  - briefly changes to a green checkmark
  - used for IDs, JSON bodies, URLs, file paths, and credentials where display is allowed
- [x] Same title casing:
  - use `Title Casing Like This` for page titles, card titles, modal titles, and table labels
- [x] Same tooltip policy:
  - every link, button, textbox, dropdown, toggle, icon, table filter, chart range control, and row action has a descriptive tooltip
  - tooltips explain how the control is used, not just the control name

## Repository And Project Layout

Keep code within the approved repository surfaces.

- [x] Add local dashboard frontend code under `dashboard/src/local/`.
- [x] Move shared dashboard UI into reusable modules when needed:
  - `dashboard/src/components/`
  - `dashboard/src/i18n/`
  - `dashboard/src/utils/`
- [x] Avoid duplicating large chunks from `dashboard/src/App.jsx`; extract shared components first when reuse is meaningful.
- [x] Add a Vite local-dashboard entry point:
  - `dashboard/src/local/main.jsx`
  - `dashboard/src/local/LocalDashboardApp.jsx`
  - `dashboard/src/local/localApi.js`
- [x] Add a build output for the local dashboard:
  - preferred: `dashboard/dist-local/`
  - embedded or copied into `src/Xeno.Client` during build
- [x] Add local web service code under `src/Xeno.Client/LocalDashboard/`.
- [x] Add one C# class per file for every local API model, settings type, service, and DTO.
- [x] Update build scripts so the local dashboard bundle is available when packing the `xeno` tool.

## Client Settings

Add a first-class settings group to `ClientSettings`.

```json
{
  "LocalDashboard": {
    "Enabled": true,
    "Host": "127.0.0.1",
    "Port": 9343,
    "OpenBrowserOnStart": false,
    "RequireLocalToken": true,
    "Database": {
      "Filename": "~/.xeno/local-dashboard.db",
      "ConnectionString": null
    },
    "HistoryRetention": {
      "MaxEntries": 1000,
      "MaxBodyBytes": 65536,
      "PersistBodies": true
    }
  }
}
```

Implementation rules:

- [x] Create `ClientLocalDashboardSettings`.
- [x] Create `ClientLocalDatabaseSettings`.
- [x] Create `ClientLocalHistorySettings`.
- [x] Default host must be `127.0.0.1`.
- [x] Support `localhost`, `127.0.0.1`, `[::1]`, and explicit `0.0.0.0`.
- [x] Treat `0.0.0.0` as an intentional all-interface bind, not the default.
- [x] When `0.0.0.0` is configured, CLI output and settings UI must clearly show the effective access URLs and warn that the dashboard may be reachable from other machines depending on firewall/network policy.
- [x] Reject `+`, `*`, malformed host values, and ambiguous wildcard forms other than explicit `0.0.0.0`.
- [x] Default port should be stable and documented. Use `9343` unless it conflicts with an existing Xeno convention.
- [x] Default SQLite filename should be `~/.xeno/local-dashboard.db` unless an existing Xeno convention suggests a better name.
- [x] SQLite filename must be configurable in `client.json` under `LocalDashboard.Database.Filename`.
- [x] Optional SQLite connection string must be configurable in `client.json` under `LocalDashboard.Database.ConnectionString`; when supplied, it overrides `Filename`.
- [x] If the port is unavailable, fail the local dashboard start with a clear log message. Do not silently bind a random port unless a future setting explicitly enables fallback ports.
- [x] Mask credentials in all local API responses.
- [x] Never return `Server.SecretKey`, provider credentials, or REST tokens in raw form.
- [x] Add CLI commands later only if needed; the first implementation can rely on editing `client.json` and the local dashboard Settings page.

## CLI Output And Discovery

The command line should make the local dashboard discoverable without being noisy. Only mention the local dashboard when `LocalDashboard.Enabled` is `true`; disabled installations should keep the current concise command output.

- [x] `xeno connect` successful background start output includes:
  - process ID
  - log file path
  - local dashboard URL when enabled and successfully started
  - a short sentence such as `Local dashboard: http://127.0.0.1:9343`
- [x] `xeno connect` timeout or partial-start output includes:
  - process ID
  - log file path
  - local dashboard state when enabled, even if WebSocket registration failed
  - local dashboard URL only if the service is actually listening
- [x] `xeno connect --foreground` prints the local dashboard URL when enabled and started.
- [x] `xeno start` mirrors `xeno connect` output because it is an alias.
- [x] `xeno status` includes a Local Dashboard section when enabled:
  - Enabled
  - Listening
  - URL
  - Host
  - Port
  - Last Started UTC
  - Last Error
- [x] `xeno status` shows `Local Dashboard: disabled` as one compact line when disabled, not a large empty section.
- [x] `xeno logs` captures local dashboard startup, URL, shutdown, and errors with timestamp/severity.
- [x] `xeno config show` includes local dashboard settings and the effective URL when enabled.
- [x] `xeno help`, `xeno connect --help`, `xeno status --help`, and `xeno config --help` mention how the dashboard URL is discovered.
- [x] Add a small `xeno dashboard` or `xeno dashboard open` command only if it is straightforward and cross-platform:
  - prints the URL
  - optionally opens the browser
  - returns a clear message if the dashboard is disabled or not running
- [x] All CLI output remains formatted text by default and JSON only when `--json` is supplied.

## Background Worker Lifecycle

The local web service runs only inside the background worker process. Foreground mode can optionally run it when enabled, but background mode is the required path.

- [x] Load settings before starting the worker.
- [x] When `LocalDashboard.Enabled` is `true`, start the local web service before or immediately after WebSocket connection starts.
- [x] Local dashboard startup must not block WebSocket connection indefinitely.
- [x] If local dashboard startup fails:
  - log a clear `[ERROR]` line
  - keep the Xeno client WebSocket worker running
  - set local dashboard status in runtime state
- [x] Stop the local web service during client shutdown.
- [x] Dispose Watson server and file watchers cleanly.
- [x] Add local dashboard URL to `xeno status`.
- [x] Add local dashboard URL to successful `xeno connect` output when enabled.
- [x] Add local dashboard state to failed or timed-out `xeno connect` output when enabled.
- [x] Add local dashboard startup and shutdown lines to `xeno logs` with timestamp and severity.

## Local SQLite Database

Request history must persist in a local SQLite database. A JSON file is not sufficient because the local dashboard will need analytics over past requests processed by the client, including time-window summaries, model/provider breakdowns, latency distributions, failure rates, and streaming metrics. The implementation should follow the same database architecture principles used by the Xeno server and the `c:\code\agents\requirements` backend guidance: provider driver classes, method interfaces, startup migrations, provider-specific SQL, sanitization helpers where useful, and parameterized commands for all values.

- [x] Add local database code under `src/Xeno.Client/LocalDashboard/Database/`.
- [x] Use one class or enum per file.
- [x] Create `LocalDashboardDatabaseDriverBase`.
- [x] Create `LocalDashboardDatabaseDriverFactory`.
- [x] Create `LocalDashboardDatabaseSettings` or use `ClientLocalDatabaseSettings` consistently; do not duplicate settings models without a clear boundary.
- [x] Create provider folder `Sqlite/`.
- [x] Create `SqliteLocalDashboardDatabaseDriver`.
- [x] Create `Sqlite/Implementations/`.
- [x] Create `Sqlite/Queries/`.
- [x] Create `Sqlite/Sanitizer.cs` if dynamic identifiers or sortable columns are needed.
- [x] Do not use string concatenation for user-provided values in SQL.
- [x] Use SQLite parameters for every value:
  - `$id`
  - `$requestId`
  - `$providerName`
  - `$modelName`
  - `$fromUtc`
  - `$toUtc`
  - `$limit`
- [x] If table or column names must be selected dynamically, whitelist them through a sanitizer instead of accepting raw input.
- [x] Open SQLite with a connection string built from the configured filename unless `ConnectionString` is supplied.
- [x] Create the database directory automatically.
- [x] Run schema migrations on local dashboard startup before serving request-history APIs.
- [x] Store applied migrations in `schema_migrations`.
- [x] Migrations must be idempotent.
- [x] Use UTC ISO-8601 text timestamps or another documented SQLite-compatible timestamp representation consistently.
- [x] Add indexes needed for analytics:
  - request received UTC
  - completed UTC
  - model name
  - provider name
  - outcome/status
  - request path
  - requestor IP
- [x] Keep the schema ready for future analytics; avoid only storing opaque JSON blobs.

Method interfaces:

- [x] `ILocalRequestHistoryMethods`
- [x] `ILocalRequestSummaryMethods`
- [x] `ILocalProviderSnapshotMethods`
- [x] `ILocalModelSnapshotMethods`
- [x] `ILocalDashboardMetadataMethods`

Required tables:

- [x] `local_request_history`
- [x] `local_request_headers`
- [x] `local_response_headers`
- [x] `local_request_bodies`
- [x] `local_response_bodies`
- [x] `local_request_timeline`
- [x] `local_request_telemetry`
- [x] `local_provider_snapshots`
- [x] `local_model_snapshots`
- [x] `local_dashboard_metadata`
- [x] `schema_migrations`

Retention and pruning:

- [x] Apply `HistoryRetention.MaxEntries` through database pruning, not in-memory list trimming.
- [x] Prune oldest request records and all dependent rows transactionally.
- [x] Honor `HistoryRetention.MaxBodyBytes` before writing body payloads.
- [x] Honor `HistoryRetention.PersistBodies`; when false, store metadata and omit body rows.
- [x] Expose retained-count and oldest/newest timestamps in local dashboard statistics.

## Local Web Service

Use Watson 7 for the localhost service, with a small typed API and static asset hosting for the React bundle.

Routes:

- [x] `GET /` serves the local dashboard SPA.
- [x] `GET /assets/*` serves built dashboard assets with cache headers.
- [x] `GET /health` returns local service health.
- [x] `GET /api/v1/state` returns connectedness state, process metadata, dashboard settings summary, and current client identity.
- [x] `GET /api/v1/statistics` returns local request counters and latency aggregates.
- [x] `GET /api/v1/providers` lists configured provider endpoints.
- [x] `POST /api/v1/providers` creates a provider endpoint.
- [x] `PUT /api/v1/providers/{name}` updates a provider endpoint.
- [x] `DELETE /api/v1/providers/{name}` deletes a provider endpoint.
- [x] `GET /api/v1/models` lists configured advertised models, flattened across providers.
- [x] `POST /api/v1/models` creates a model under a provider.
- [x] `PUT /api/v1/models/{providerName}/{modelName}` updates model capability flags.
- [x] `DELETE /api/v1/models/{providerName}/{modelName}` deletes a model.
- [x] `GET /api/v1/requests` lists local request history.
- [x] `GET /api/v1/requests/{id}` returns full local request details.
- [x] `DELETE /api/v1/requests/{id}` deletes one local history entry.
- [x] `POST /api/v1/requests/bulk` deletes filtered local history entries.
- [x] `GET /api/v1/requests/summary` returns bucketed request counts for charts.
- [x] `GET /api/v1/requests/analytics/overview` returns historical request statistics from SQLite.
- [x] `GET /api/v1/logs?tail=100&level=all` returns recent local log lines.
- [x] `GET /api/v1/settings` returns editable local dashboard and client-safe settings.
- [x] `PUT /api/v1/settings` updates safe local settings and persists `client.json`.

Security:

- [x] Bind to `127.0.0.1` by default.
- [x] Allow `0.0.0.0` only when explicitly configured.
- [x] When bound to `0.0.0.0`, require the local dashboard token and surface a visible warning in the UI and CLI output.
- [x] Deny CORS by default.
- [x] If CORS is implemented for development, allow only same-origin local dashboard requests.
- [x] Generate a per-process local dashboard token on startup when `RequireLocalToken` is true.
- [x] Inject the token into the served dashboard bootstrap payload.
- [x] Require `X-Xeno-Local-Token` on every `/api/v1/*` request.
- [x] Use `Content-Type: application/json` for mutating requests.
- [x] Return stable typed error responses with status, code, and message.
- [x] Do not expose raw provider credentials or server secret keys.
- [x] Do not expose user password material because the client should never store it.

## Local Request History

The client currently processes `ServerRequestMessage` instances and writes logs. The local dashboard needs a typed, bounded history store that records each received request and its outcome.

Create local history models:

- [x] `LocalRequestHistoryEntry`
- [x] `LocalRequestTelemetry`
- [x] `LocalRequestTimelineEvent`
- [x] `LocalRequestHistorySummaryResponse`
- [x] `LocalRequestHistorySummaryBucket`
- [x] request history filters are currently bounded to limit/range query values
- [x] request headers are captured as typed dictionaries on `LocalRequestHistoryEntry`
- [x] request body capture is bounded by `MaxBodyBytes`
- [x] response body capture is bounded by `MaxBodyBytes`

Capture fields:

- [x] Request ID
- [x] Tenant ID
- [x] Client ID
- [x] received UTC
- [x] completed UTC
- [x] method
- [x] path
- [x] requestor IP from forwarded headers
- [x] selected provider name
- [x] provider API type
- [x] model name
- [x] HTTP status code returned to the server
- [x] success/failure
- [x] error message when applicable
- [x] request headers
- [x] request body, bounded by `MaxBodyBytes`
- [x] response headers
- [x] response body, bounded by `MaxBodyBytes`
- [x] streaming flag
- [x] time to first token or first byte
- [x] total duration
- [x] total tokens when available
- [x] prompt tokens when available
- [x] completion tokens when available
- [x] stream chunk count
- [x] upstream endpoint latency when available
- [x] timeline events with UTC timestamps

Persistence:

- [x] Store request history in the configured local SQLite database.
- [x] Never store request history in a JSON file.
- [x] Insert the main request row into `local_request_history`.
- [x] Insert request headers into `local_request_headers`.
- [x] Insert response headers into `local_response_headers`.
- [x] Insert request body capture into `local_request_bodies` when enabled.
- [x] Insert response body capture into `local_response_bodies` when enabled.
- [x] Insert timeline events into `local_request_timeline`.
- [x] Insert telemetry into `local_request_telemetry`.
- [x] Use database transactions when writing or deleting a request and its related rows.
- [x] Keep only `MaxEntries` through SQLite pruning queries.
- [x] Keep body capture bounded by `MaxBodyBytes` before insert.
- [x] Redact authorization, API key, secret, token, cookie, and credential-like headers.
- [x] Redact provider credentials from captured payloads where known.
- [x] Support clearing history from the local dashboard.
- [x] Build request summaries from SQLite aggregation queries, not in-memory scans after loading all rows.
- [x] Support future analytics queries without schema replacement.

Concurrency:

- [x] Use database transactions for consistency and a small coordination lock only where settings and inventory updates require it.
- [x] Do not mutate `ClientSettings.Providers` from request handling without coordination.
- [x] Ensure dashboard edits cannot corrupt `client.json` while the WebSocket loop is reading inventory.
- [x] Re-advertise inventory to the Xeno server after provider/model changes when connected.

## Local Dashboard Pages

### Home

- [x] Show connection state:
  - connected
  - registered
  - background process ID
  - WebSocket URL
  - client ID
  - tenant ID
  - last heartbeat UTC
  - last server message UTC
  - last disconnect reason
- [x] Show a full-width Requests Over Time bar chart.
- [x] Show compact KPI cards consistent with the main dashboard:
  - requests
  - failures
  - average latency
  - streaming requests
  - configured providers
  - advertised models
- [x] Show recent requests table with row click into Request Details.
- [x] Include auto-refresh controls.

### Providers

- [x] Full-width providers table.
- [x] Columns:
  - Name
  - API Type
  - Endpoint
  - Timeout ms
  - Credential
  - Models
  - Active/Configured state if added
- [x] Create provider modal.
- [x] Edit provider modal.
- [x] Delete provider action with confirmation.
- [x] Test provider action if a lightweight connectivity check is available.
- [x] Credential field must support set, replace, and clear, but never reveal the existing secret.
- [x] Use the term `API Type`, not `Provider`, for Ollama/OpenAI/Gemini/vLLM-compatible type.

### Models

- [x] Full-width advertised models table.
- [x] Columns:
  - Model
  - Provider
  - API Type
  - Completions
  - Embeddings
  - Endpoint
- [x] Create model modal.
- [x] Edit model modal with capability controls:
  - completion
  - embedding
  - both
- [x] Delete model action with confirmation.
- [x] Filter by provider dropdown.
- [x] Avoid card stacks for model details.

### Requests

- [x] Requests Over Time chart at the top.
- [x] Range selector:
  - Last Hour
  - Last Day
  - Last Week
  - Last Month
- [x] Full-width request history table.
- [x] Columns:
  - ID
  - Received UTC
  - Method
  - Path
  - Model
  - Provider
  - Status
  - Duration
  - Requestor IP
  - Streamed
- [x] Row click opens Request Details.
- [x] Delete and View JSON actions.
- [x] Bulk clear action with confirmation.
- [x] Request Details modal:
  - consumes most of the available viewport
  - overview table, not cards
  - routing/provider table
  - telemetry table
  - timeline with timestamps
  - request headers collapsible, expanded by default
  - request body collapsible, expanded by default
  - response headers collapsible, expanded by default
  - response body collapsible, expanded by default
  - copy controls on each JSON/header/body section

### Settings

- [x] Show local dashboard settings.
- [x] Show client identity and config file paths.
- [x] Allow safe edits:
  - local dashboard enabled
  - port
  - SQLite database filename
  - open browser on start
  - history max entries
  - max body bytes
  - persist bodies
  - colors if shared with CLI
- [x] Show database status:
  - filename
  - connection state
  - schema version
  - retained request count
  - oldest request UTC
  - newest request UTC
- [x] Clearly mark restart-required settings:
  - host
  - port
  - enabled state
  - database filename
  - connection string
- [x] Use reduced-height textareas where list inputs are needed, matching the main dashboard fixes.
- [x] Include copy controls for file paths and URLs.

### Logs

- [x] Optional but recommended: add a Logs page.
- [x] Show timestamped client logs with severity.
- [x] Filter by severity.
- [x] Tail line count selector.
- [x] Follow mode if practical in the browser, using polling first unless SSE is warranted.
- [x] Preserve fixed-width log formatting.

## Frontend Implementation Details

Reuse the main dashboard design instead of recreating it.

- [x] Extract shared pieces from `dashboard/src/App.jsx` when needed:
  - `Shell`
  - `DataTable`
  - `RequestDetailsModal` patterns
  - formatter helpers
  - `useResource`
  - range helpers
  - table helpers
- [x] Keep shared components generic enough for both main and local dashboards.
- [x] Add `LocalDashboardApp.jsx` with local routes:
  - `/`
  - `/providers`
  - `/models`
  - `/requests`
  - `/settings`
  - `/logs` if implemented
- [x] Use the same `CopyButton`, `ActionMenu`, `ConfirmModal`, `LanguageSelector`, and `ActivityChart`.
- [x] Use lucide icons for all icon buttons.
- [x] Use the real GitHub icon implementation already selected for the main dashboard.
- [x] Use no marketing or landing page.
- [x] Do not use badges for IDs.
- [x] Keep text within containers at desktop and mobile widths.
- [x] Add tooltips to every interactive control.
- [x] Add localized empty states for:
  - no providers
  - no models
  - no requests
  - local service unavailable
  - history capture disabled
- [x] Add localized error states for:
  - failed settings save
  - provider validation failure
  - model validation failure
  - request history load failure

## Build And Packaging

The `xeno` tool must include the local dashboard assets after packaging.

- [x] Add a local dashboard Vite build command.
- [x] Ensure `build-dashboard.bat` still builds the main dashboard image.
- [x] Add a client/local-dashboard build path to `build-all.bat` or a dedicated script only if it does not break Docker image builds.
- [x] Copy or embed `dashboard/dist-local` into `Xeno.Client`.
- [x] If embedding:
  - add `EmbeddedResource` entries to `Xeno.Client.csproj`
  - implement static asset resolution by resource name
- [x] If copying:
  - add content files under a clear `wwwroot` path
  - ensure `dotnet pack` includes them in the tool package
- [x] Add cache headers:
  - `index.html`: no-store
  - hashed JS/CSS assets: long cache
- [x] Include favicon/logo assets from `assets/`.

## Local API Models

Create named models for every local API contract. No STJ DOM types.

- [x] `LocalDashboardStateResponse`
- [x] `LocalDashboardStatisticsResponse`
- [x] `LocalProviderListResponse`
- [x] `LocalProviderUpsertRequest`
- [x] `LocalProviderTestResponse`
- [x] provider responses use named `ClientRunnerSettings`
- [x] `LocalModelListResponse`
- [x] `LocalModelUpsertRequest`
- [x] `LocalModelEntry`
- [x] `LocalRequestHistoryListResponse`
- [x] request history detail responses use named `LocalRequestHistoryEntry`
- [x] `LocalRequestHistorySummaryResponse`
- [x] `LocalDashboardMutationResponse`
- [x] `LocalDashboardSettingsResponse`
- [x] `LocalDashboardDatabaseResponse`
- [x] `LocalDashboardDatabaseSettingsUpdateRequest`
- [x] `LocalDashboardLogResponse`
- [x] `LocalDashboardErrorResponse`

Enums:

- [x] `LocalDashboardStatusEnum`
- [x] `LocalDashboardDatabaseStatusEnum`
- [x] `LocalRequestOutcomeEnum`
- [x] `LocalRequestTimelineStageEnum`
- [x] `LocalProviderCredentialStateEnum`
- [x] `LocalModelCapabilityEnum`

## Client Runtime Services

Add small, testable services rather than adding more responsibility to `Program.cs`.

- [x] `LocalDashboardService`
  - owns Watson server lifecycle
  - serves static assets
  - registers local API routes
- [x] Local dashboard route registration
  - maps local API routes to handlers inside `LocalDashboardService`
- [x] `LocalDashboardAssetService`
  - resolves embedded/copied SPA assets
  - content type lookup
  - cache policy
- [x] Client settings operations
  - validates and saves settings inside `LocalDashboardService`
  - provider/model operations
  - coordination lock
- [x] `LocalDashboardDatabaseService`
  - opens SQLite through the configured filename or connection string
  - runs migrations
  - exposes method interfaces to higher-level services
- [x] `LocalRequestHistoryStore`
  - appends and updates request records through `ILocalRequestHistoryMethods`
  - delegates summary queries to `ILocalRequestSummaryMethods`
  - prunes bounded history through database methods
- [x] `LocalStatisticsService`
  - aggregates local counters from SQLite summary methods
- [x] Provider snapshot persistence
  - writes provider snapshots to SQLite after provider configuration changes
- [x] Model snapshot persistence
  - writes model snapshots to SQLite after model configuration changes
- [x] `LocalSecretRedactor`
  - redacts sensitive headers and known fields
- [x] `LocalDashboardTokenService`
  - generates and validates local token

## Integration Points In Existing Client Flow

Touch the request path deliberately.

- [x] Start local dashboard service from `RunForegroundConnectAsync` when worker mode is active and settings enable it.
- [x] Record `LocalRequestHistoryEntry` when `ServerRequestMessage` is received.
- [x] Update history when response starts.
- [x] Append chunk telemetry during streaming.
- [x] Complete history when final response is sent.
- [x] Complete failed history entries when upstream/provider calls fail.
- [x] Include requestor IP in history from forwarded headers.
- [x] Update provider/model changes so future inventory advertisements reflect the saved config.
- [x] Trigger a re-register or inventory update message after local provider/model edits when connected.

## Documentation Updates

The documentation update is part of the feature, not cleanup after the fact. A new user following the client docs should understand how to enable the local dashboard, how to reach it after `xeno connect`, what it can edit, what it cannot edit, and where sensitive values are redacted.

- [x] Update root `README.md`:
  - describe the local client dashboard in the client capability overview
  - explain that it defaults to `127.0.0.1`, can explicitly bind to `0.0.0.0`, and is disabled/enabled through client settings
  - warn that `0.0.0.0` may expose the dashboard beyond the local machine
  - show the relationship between main server dashboard and local client dashboard
  - include a short `xeno connect` example showing the local dashboard URL when enabled
  - link to `CLIENT_GETTING_STARTED.md`
- [x] Update `CLIENT_GETTING_STARTED.md` thoroughly:
  - how to enable the local dashboard
  - the complete `LocalDashboard` settings JSON block with defaults
  - how to set `LocalDashboard.Database.Filename`
  - how SQLite persistence is used for request history and analytics
  - which settings require restarting the background client
  - how `xeno connect` reports the local dashboard URL when enabled
  - how `xeno status` reports whether the local dashboard is listening
  - how `xeno logs` records local dashboard startup and errors
  - how to find the dashboard URL
  - how `0.0.0.0` changes bind behavior and what the security tradeoff is
  - how to open it manually in a browser
  - provider/model editing through the dashboard
  - request history and request detail inspection
  - local database backup/reset considerations
  - history retention and redaction caveats
  - troubleshooting for port conflicts, disabled dashboard, token errors, and blank browser pages
- [x] Add or update `src/Xeno.Client/README.md` if the tool package README diverges from the root README:
  - include local dashboard quick start
  - include command output examples
  - include settings defaults
- [x] Update any dashboard README that explains main-dashboard behavior to clarify that the main dashboard and local dashboard are separate surfaces sharing a design system.
- [x] Update SDK README files only if they mention client setup or local debugging:
  - C# SDK README
  - JavaScript SDK README
  - Python SDK README
  - make clear SDKs call the Xeno server, while the local dashboard observes one client
- [x] Update `WEBSOCKETS_API.md` only if inventory update or re-register behavior changes.
- [x] Update `REST_API.md` only if local API docs are intended to be public; otherwise keep local API docs in this file or a future dedicated local API document.
- [x] Update `CHANGELOG.md`.
- [x] Document settings schema with defaults and restart-required fields.
- [x] Document the local SQLite schema at a user-facing level:
  - what is stored
  - what is redacted
  - where the file lives
  - how retention pruning works
  - why the database is needed for historical analytics
- [x] Add CLI help examples to docs:
  - `xeno connect`
  - `xeno start`
  - `xeno status`
  - `xeno config show`
  - optional `xeno dashboard open` if implemented

## Testing Plan

Backend and CLI tests:

- [x] Add Touchstone tests for settings load/save with `LocalDashboard`.
- [x] Add Touchstone tests that invalid hosts are rejected.
- [x] Add Touchstone tests that loopback hosts are accepted.
- [x] Add Touchstone tests that explicit `0.0.0.0` is accepted and reported as an all-interface bind.
- [x] Add Touchstone tests that wildcard host forms other than explicit `0.0.0.0` are rejected.
- [x] Add Touchstone tests that `LocalDashboard.Database.Filename` is loaded, expanded, and persisted correctly.
- [x] Add Touchstone tests that SQLite database directories are created automatically.
- [x] Add Touchstone tests that local SQLite migrations are idempotent.
- [x] Add Touchstone tests that request history writes use parameterized inputs, including special characters in path, model, provider, and requestor IP.
- [x] Add Touchstone tests that dynamic filter/sort fields are sanitized or rejected.
- [x] Add Touchstone tests for local dashboard token validation.
- [x] Add Touchstone tests for provider create/update/delete through local API handlers.
- [x] Add Touchstone tests for model create/update/delete through local API handlers.
- [x] Add Touchstone tests for request history append/update/summary.
- [x] Add Touchstone tests for request-history pruning by `MaxEntries`.
- [x] Add Touchstone tests for request body retention by `MaxBodyBytes` and `PersistBodies`.
- [x] Add Touchstone tests for redaction of authorization and credential headers.
- [x] Add Touchstone tests that local dashboard startup failure does not stop WebSocket connection.
- [x] Add Touchstone tests that `xeno status` includes local dashboard state when enabled.
- [x] Add Touchstone tests that `xeno connect` logs dashboard startup URL when enabled.
- [x] Add Touchstone tests that `xeno connect` output includes dashboard access instructions only when enabled.
- [x] Add Touchstone tests that `xeno status` uses compact disabled output when the local dashboard is disabled.
- [x] Add Touchstone tests that `xeno config show` includes local dashboard settings and effective URL.

Frontend tests and checks:

- [x] Run `npm.cmd run build` for the main dashboard.
- [x] Run the local dashboard Vite build.
- [x] Run i18n verification for shared and local translation keys.
- [x] Add a lightweight render or static check for local dashboard route labels if the project already supports it.
- [x] Manually inspect light and dark modes.
- [x] Manually inspect mobile and desktop widths.
- [x] Verify chart hover tooltip position in the local dashboard.
- [x] Verify all table action menus render above clipping boundaries.
- [x] Verify every interactive element has a descriptive tooltip.

End-to-end smoke:

- [x] Start Xeno server locally.
- [x] Configure Xeno client with a local provider and model.
- [x] Enable local dashboard.
- [x] Run `xeno connect`.
- [x] Confirm background worker registers.
- [x] Open local dashboard.
- [x] Confirm Home shows connected and registered.
- [x] Edit provider.
- [x] Add model.
- [x] Send a chat completion through Xeno server.
- [x] Confirm request appears in local dashboard request history.
- [x] Click request and confirm full details, headers, body, telemetry, and timeline.
- [x] Confirm chart updates.
- [x] Stop client and confirm local dashboard service stops.

## UX Review Passes

Run these passes before marking complete.

Automated implementation work is complete as of the latest update. Browser UX validation is covered by the Playwright local-dashboard check in `dashboard/tests/local-dashboard-ux.spec.mjs`.

### Pass 1: Visual Parity

- [x] Compare local dashboard and main dashboard side by side.
- [x] Confirm shell, top bar, nav, cards, tables, modals, charts, copy controls, and buttons match.
- [x] Confirm no local page uses a design pattern already rejected in the main dashboard:
  - no useless administration cards
  - no data point card stacks in details modals
  - no ID badges
  - no clipped menus
  - no high-contrast dark-mode table hover

### Pass 2: Workflow Usability

- [x] A user can understand whether the client is connected within five seconds.
- [x] A user can add a provider without editing JSON.
- [x] A user can add a model and specify completion/embedding capability.
- [x] A user can inspect a request without reading logs.
- [x] A user can clear history intentionally, with confirmation.
- [x] A user can copy IDs, JSON bodies, URLs, and file paths from the UI.

### Pass 3: Requirements Compliance

- [x] No STJ DOM types were introduced.
- [x] No tuples were introduced.
- [x] No `var` declarations were introduced in C#.
- [x] Every public class/member/method has XML docs.
- [x] Async methods accept cancellation tokens.
- [x] Local API uses named request/response classes.
- [x] Local persistence uses SQLite through database interfaces and implementations.
- [x] SQLite access is parameterized and sanitizer-whitelisted where dynamic identifiers are required.
- [x] Local schema migrations run on startup and are idempotent.
- [x] Tests use Touchstone packages and shared descriptors.
- [x] Frontend strings are localized.
- [x] Charts are hand-rolled SVG.
- [x] The service defaults to `127.0.0.1` and only binds to `0.0.0.0` when explicitly configured.
- [x] Sensitive values are redacted.

## Completion Definition

The feature is complete when:

- [x] `xeno connect` starts a background client and, when enabled, starts a dashboard service bound to `127.0.0.1` by default.
- [x] `xeno connect` and `xeno start` tell the user how to access the local dashboard only when it is enabled.
- [x] `xeno status` shows the local dashboard state and URL.
- [x] `xeno config show` shows local dashboard settings and effective URL.
- [x] Local SQLite database filename is configurable in `client.json`.
- [x] Local SQLite schema is migrated on client startup.
- [x] The local dashboard renders with the same UX language as the main dashboard.
- [x] Providers can be created, edited, deleted, and tested from the local dashboard.
- [x] Models can be created, edited, and deleted from the local dashboard.
- [x] Request history is captured locally with telemetry, headers, bodies, and timeline.
- [x] Request history is persisted in SQLite and can be queried for historical analytics.
- [x] Request history chart and table update through auto-refresh.
- [x] Request details are complete, readable, and copyable.
- [x] Settings edits persist safely to `client.json`.
- [x] Sensitive values remain masked.
- [x] All required tests pass.
- [x] Root README, client getting-started docs, package/client README material, and any affected SDK README references are updated.
- [x] Manual UX parity review has been completed against the main dashboard.

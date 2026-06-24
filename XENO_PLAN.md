# Xeno Plan

Status date: 2026-06-24
Version target: v0.1.0 alpha

Xeno is an AI control plane with a Watson 7 C# server, a React dashboard, a cross-platform `xeno` client, and Docker deployment assets. Connected clients advertise hardware and available models. Routing sends OpenAI-compatible completion and embedding requests directly to a healthy connected client that advertises the requested model.

## Current Product Decisions

- The backend is multitenant.
- Tenants, users, credentials, roles, permissions, audit records, clients, model definitions, model configurations, request history, analytics, settings, backup, and restore are first-class server resources.
- A user may run `xeno login -u {username}`. The client prompts for the password, exchanges it for an access key and secret, and stores only that client credential.
- WebSocket client connections require `x-access-key` plus `x-secret-key`; user passwords and bearer tokens are rejected for WebSocket control-plane connections.
- The dashboard is an operator console, not a marketing page.
- Connected clients are the execution units. There is no separate execution endpoint resource.
- Request distribution is direct client selection by healthy connection, model availability, capacity, and current in-flight count.
- Server settings are editable from the dashboard and persisted to the same JSON settings file used at server startup. Listener and database changes may require restart to fully apply.
- Docker defaults to PostgreSQL with an initialization container for the expected role and database.

## Repository Layout

- `/README.md`: v0.1.0 alpha overview, quick start, Docker, client login notes.
- `/CHANGELOG.md`: release notes.
- `/REST_API.md`: REST and OpenAI-compatible API summary.
- `/LICENSE`: MIT.
- `/AGENTS.md`: local agent guidance.
- `/XENO_PLAN.md`: this implementation plan.
- `/Xeno.postman_collection.json`: Postman requests for supported routes.
- `/build-server.bat`: builds and pushes `jchristn77/xeno-server:{tag}` and `latest`.
- `/build-dashboard.bat`: builds and pushes `jchristn77/xeno-dashboard:{tag}` and `latest`.
- `/build-all.bat`: accepts a Docker image tag and delegates to server and dashboard build scripts.
- `/src`: C# solution, server, client, core library, and Touchstone test projects.
- `/dashboard`: React/Vite dashboard.
- `/docker`: compose files, settings, nginx assets, PostgreSQL init assets.
- `/docker/factory`: reset scripts.
- `/assets`: logo and favicon assets used by docs and dashboard.

## Implementation Status

| Area | Status | Current state | Remaining work |
| --- | --- | --- | --- |
| Server host | In progress | Watson 7 server, CORS, OpenAPI, Swagger, health, metrics, REST dispatcher, WebSocket gateway. | Split the large server class into route modules and typed DTO handlers. |
| Multitenancy | In progress | Tenant-scoped platform state, seeded tenant, users, credentials, roles, permissions, audit records; create/update/delete routes for core administration records. | Add typed DTO route modules and richer audit evidence for administrative writes. |
| Authentication | In progress | Admin key, credential auth, bearer token issuance, password login issuing client credentials, WebSocket credential gate. | Add credential rotation/revocation workflows and password reset/change flows. |
| Clients | In progress | Clients register over WebSocket and advertise provider endpoints plus available models. Heartbeats update connection state. | Add richer hardware inventory, GPU memory, model metadata, and provider health probes. |
| Routing | In progress | Completion and embedding requests route directly to connected client candidates by tenant, model, health, capacity, and in-flight count. | Add persistent request correlation and richer route evidence in request history. |
| OpenAI API | In progress | `/v1/models`, `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`, and `/api/v1/openai/v1/...` aliases. | Improve streaming, token accounting, provider error normalization, and request redaction. |
| Request history | In progress | Request history list, summary buckets, bulk clear, analytics overview, Prometheus-style metrics. | Add retention cleanup, advanced search filters, TTFT, token counts, and cost estimates. |
| Settings | In progress | `GET/POST /api/v1/settings` and `/v1.0/api/settings` read and persist server JSON config. Dashboard has typed controls and raw JSON editor. | Add restart warnings per field and safe reload support for settings that can apply without restart. |
| Dashboard shell | In progress | Logo, sidebar, GitHub/theme/logout controls, plain server URL with copy, Spanish language selector, reusable modals, copy components, tables, pagination, charts. | Add toast provider, loading/error states, and interaction tests. |
| Dashboard pages | In progress | Home, request history, API Explorer, analytics, clients, advertised models, tenants, users, credentials, security, and settings. Tenants, users, credentials, roles, and permissions have persisted create/edit modals. | Add create forms for clients/model definitions if product requirements call for manual creation, plus permission-aware disabled states. |
| API Explorer | In progress | Integrated request builders for chat completions, completions, embeddings, and models. | Add saved examples, response timing, auth header display, and streaming toggle. |
| Docker | In progress | Server/dashboard Dockerfiles, PostgreSQL default compose, initialization container, factory reset scripts. | Add deployment docs for TLS, external databases, and production secrets. |
| Client | In progress | Console client initializes settings, logs in, stores access key/secret, connects WebSocket, advertises inventory, and handles requests. | Add packaged tool publishing matrix and stronger provider configuration validation. |
| Testing | In progress | Touchstone shared suites run through automated, xUnit, and NUnit projects. Routing, database, auth, WebSocket, management delete, management update, and inventory smoke coverage exists. | Add dashboard interaction tests and provider emulator coverage for OpenAI-compatible endpoints. |
| Documentation | In progress | README, REST_API, Postman, dashboard audit, and this plan updated for v0.1.0 alpha. | Add complete endpoint examples and troubleshooting guides. |

## Server Plan

1. Keep `Xeno.Core` as the domain/state library.
2. Keep `Xeno.Server` as the Watson host.
3. Keep `Xeno.Client` as the cross-platform console client.
4. Move REST route handlers from the monolithic `XenoServer` class into route modules:
   - authentication;
   - tenants;
   - users;
   - credentials;
   - roles and permissions;
   - clients;
   - model definitions and configurations;
   - request history;
   - analytics;
   - settings;
   - backup and restore;
   - OpenAI-compatible API.
5. Add typed request and response DTOs for every REST action.
6. Enforce tenant scope in every route.
7. Append audit records for authentication, authorization, write, delete, and administrative actions.
8. Persist platform state through SQLite, PostgreSQL, SQL Server, and MySQL drivers.
9. Keep the settings JSON shape aligned with:
   - `Rest`;
   - `Cors`;
   - `WebSocket`;
   - `Database`;
   - `Logging`;
   - `Auth`;
   - `RequestHistory`;
   - `Routing`.
10. Mark settings updates that require restart in the response.

## Routing Plan

1. Parse requested model from OpenAI-compatible request payload.
2. Resolve the effective model from tenant model definitions or client-advertised model names.
3. Filter clients by tenant.
4. Require client `Active == true`.
5. Require client service state `Normal`.
6. Require a connected client with a fresh heartbeat.
7. Require client model inventory to contain the effective model.
8. Require client capacity when `MaxParallelRequests` is nonzero.
9. Select the client with the lowest `InFlightRequests`, then highest `Weight`, then stable name ordering.
10. Dispatch request to the selected client over WebSocket.
11. Record request history with selected client, effective model, HTTP method, path, status code, duration, and route timeline.

## Dashboard Plan

1. Keep the shell dense and operational:
   - logo in upper-left;
   - grouped sidebar;
   - page title and description;
   - plain server URL with copy action;
   - language selector;
   - GitHub icon;
   - light/dark toggle;
   - logout icon.
2. Use shared dashboard primitives:
   - `Modal`;
   - `ConfirmModal`;
   - `ActionMenu`;
   - `CopyButton`;
   - `CopyableValue`;
   - `EmptyState`;
   - `Pagination`;
   - `ActivityChart`;
   - `DataTable`.
3. Keep IDs and keys fixed-width with copy controls wherever they appear in tables or detail/edit views.
4. Render action menus in a portal above table overflow.
5. Open edit modals on table row click.
6. Put pagination and refresh controls above all tables.
7. Keep chart controls, axis labels, hover tooltips, and range selection visible.
8. Stack clients, advertised models, and security tables at full workspace width.
9. Provide a first-class API Explorer rather than a wrapper around the OpenAPI document.
10. Provide a full settings interface:
    - REST host, port, TLS, auth toggle;
    - CORS enablement, origins, headers, methods;
    - WebSocket enablement, path, heartbeat, request timeout;
    - database provider and connection fields;
    - logging fields;
    - raw JSON editor;
    - backup and restore editor.

## Client Plan

1. Installable command name: `xeno`.
2. Settings path: `~/.xeno/client.json`.
3. Commands:
   - `init`;
   - `login -u {username}`;
   - `status`;
   - `inventory`;
   - provider endpoint configuration commands;
   - model add/remove/list commands;
   - connect/run command.
4. `login` prompts for password and stores only access key plus secret.
5. WebSocket connect retries automatically after disconnect.
6. Client registration advertises:
   - hostname;
   - CPU;
   - GPU;
   - memory;
   - configured provider endpoints;
   - available models;
   - supported completions and embeddings.
7. Supported provider types:
   - Ollama;
   - OpenAI;
   - Gemini;
   - OpenAI-compatible endpoint such as vLLM.

## Docker Plan

1. Server image: `jchristn77/xeno-server`.
2. Dashboard image: `jchristn77/xeno-dashboard`.
3. Initial image tag: `v0.1.0`.
4. `build-server.bat {tag}` builds and pushes server `{tag}` and `latest`.
5. `build-dashboard.bat {tag}` builds and pushes dashboard `{tag}` and `latest`.
6. `build-all.bat {tag}` calls both subordinate build scripts.
7. Default compose uses PostgreSQL.
8. PostgreSQL init container creates the expected role and database before server startup.
9. Factory reset scripts remove containers, volumes, generated settings, and local data.

## Testing Plan

1. Use Touchstone packages in the same pattern as the reference apps:
   - `Test.Shared`;
   - `Test.Automated`;
   - `Test.Xunit`;
   - `Test.Nunit`.
2. Keep shared suites for:
   - settings loading;
   - database providers;
   - routing;
   - tenant isolation;
   - authentication and authorization;
   - WebSocket relay and credential gate;
   - management API smoke tests.
3. Add dashboard tests for:
   - i18n key parity;
   - build success;
   - table filtering/sorting/pagination;
   - row-click edit modals;
   - copy-to-clipboard state;
   - chart hover/range controls;
   - settings save flow.
4. Add Docker smoke tests:
   - compose starts PostgreSQL;
   - init container creates role/database;
   - server health becomes healthy;
   - dashboard serves static app;
   - OpenAI-compatible model list route responds.

## Remaining Work Before v0.1.0 Alpha Cut

1. Add create flows for clients and model definitions only if product requirements call for manual creation.
2. Add typed DTOs for administrative update/create routes and move handlers out of the monolithic server class.
3. Add dashboard toast/alert provider and richer API error surfacing.
4. Add provider emulator tests for OpenAI-compatible request relay.
5. Add settings restart-warning UI per changed field.
6. Add Docker compose smoke test.
7. Add dashboard interaction tests for edit modals, action menus, charts, and copy controls.
8. Run dashboard i18n check and production build.
9. Build Docker images with `build-all.bat v0.1.0`.
10. Push commits and image tags.

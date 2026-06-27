# Xeno REST API

Base URL: `http://localhost:9000`

The server exposes Watson 7 routes with `/openapi.json` and `/swagger`. Tenant-scoped calls accept `x-tenant-guid`; if omitted, the seeded `ten_default` tenant is used. Swagger UI includes a Xeno authentication panel that loads saved tenant, bearer token, `x-token`, access key, and secret key material into Try it out requests.

## System

`GET /health`

`GET /v1.0/api/health`

`GET /openapi.json`

`GET /swagger`

`GET /metrics`

## SDKs

C#, JavaScript, and Python SDKs are checked in under `src/sdk/`. Each SDK includes its own `README.md`, `TESTING.md`, automated tests, and an interactive console harness.

The live OpenAPI document remains available at `GET /openapi.json` and should be treated as the contract source of truth when validating or regenerating SDK coverage.

## Authentication

`GET /api/v1/auth/tenants`

`GET /v1.0/api/auth/tenants`

`POST /api/v1/auth/dashboard/login`

`POST /v1.0/api/auth/dashboard/login`

`POST /api/v1/auth/token`

`POST /v1.0/api/auth/token`

`POST /api/v1/auth/login`

`POST /v1.0/api/auth/login`

`GET /v1.0/api/auth/token/details`

`POST /v1.0/api/auth/token/refresh`

`POST /v1.0/api/auth/logout`

Token issuance accepts direct credential fields. Bearer and `x-token` values must match when both are supplied.

Login accepts `username`, `password`, and optional `tenantId`. It returns a generated `accessKey` and `secretKey` for client use. The user password is only accepted by the login endpoint and is never accepted on the WebSocket control plane.

Dashboard login is a multistep flow. The dashboard first resolves the server URL and user email, retrieves matching tenants, prompts for tenant only when more than one tenant is available, and then submits the password.

## Administration

`GET /v1.0/api/tenants`

`POST /v1.0/api/tenants`

`PUT /v1.0/api/tenants/{id}`

`DELETE /v1.0/api/tenants/{id}`

`GET /v1.0/api/users`

`POST /v1.0/api/users`

`PUT /v1.0/api/users/{id}`

`DELETE /v1.0/api/users/{id}`

`GET /v1.0/api/credentials`

`POST /v1.0/api/credentials`

`PUT /v1.0/api/credentials/{id}`

`DELETE /v1.0/api/credentials/{id}`

`GET /v1.0/api/roles`

`POST /v1.0/api/roles`

`PUT /v1.0/api/roles/{id}`

`DELETE /v1.0/api/roles/{id}`

`GET /v1.0/api/permissions`

`POST /v1.0/api/permissions`

`PUT /v1.0/api/permissions/{id}`

`DELETE /v1.0/api/permissions/{id}`

`GET /v1.0/api/effective-permissions`

`GET /v1.0/api/audit`

`DELETE /v1.0/api/audit/{id}`

`GET /v1.0/api/clients`

The clients response includes connection state, capacity fields, and each client's advertised `models` inventory.

`DELETE /v1.0/api/clients/{id}`

`DELETE /v1.0/api/client-models`

`GET /v1.0/api/model-definitions`

`GET /v1.0/api/model-configurations`

`GET /v1.0/api/routing-policies`

`POST /v1.0/api/routing-policies`

`PUT /v1.0/api/routing-policies/{id}`

`DELETE /v1.0/api/routing-policies/{id}`

`GET /v1.0/api/analytics/overview`

`GET /v1.0/api/settings`

`POST /v1.0/api/settings`

`GET /v1.0/api/backup`

`POST /v1.0/api/restore`

Create and update request bodies:

- Tenant create/update: `name`, `active`. Create may also include `id`; if omitted, the server generates a compact PrettyId with `ten_` prefix.
- User create/update: `username`, `email`, `isAdmin`, `isTenantAdmin`, `active`, and `password`. Password is required on create and optional on update. User responses are redacted and do not expose password material.
- Credential create/update: `userId`, `accessKey`, `secretKey`, and `active`. Create generates access/secret keys when omitted and returns the secret once. List/update responses redact the secret key.
- Role create/update: `name`, `permissionIds`, and `active`.
- Permission create/update: `resourceType`, `operationType` (`Read`, `Write`, `Delete`, `Admin`), `permit`, and `active`.
- Routing policy create/update: `name`, `modelName`, `clientName`, `requestTimeoutMs`, `loadBalancingMode`, `adaptive`, and `active`. Use `*` for `modelName` or `clientName` to match all values. Each tenant has an undeletable default `*/*` policy using `LeastRecentlyUsed`.

Legacy dashboard aliases under `/api/v1/...` are also registered for administration, clients, token bootstrap, settings, request history, and OpenAI-compatible routes.

## OpenAI-Compatible API

`GET /v1/models`

`POST /v1/chat/completions`

`POST /v1/completions`

`POST /v1/embeddings`

OpenAI-compatible aliases are also available under `/api/v1/openai/v1/...`.

Routed OpenAI-compatible responses include Xeno trace headers for debugging model placement and node selection:

- `X-Client-ID`: selected connected client/node ID.
- `X-Xeno-Request-ID`: Xeno request trace ID.
- `X-Xeno-Request-History-ID`: persisted request-history entry ID.
- `X-Xeno-Model`: effective model used for routing.
- `X-Xeno-Routing-Outcome`: routing outcome such as `Completed`, `ModelNotFound`, or `NoEligibleClients`.
- `X-Xeno-Routing-Message`: human-readable routing result.
- `X-Xeno-Routing-Policy-ID`, `X-Xeno-Routing-Policy-Name`, `X-Xeno-Routing-Policy-Match`, and `X-Xeno-Routing-Policy-Reason`: policy selected for this request and why it matched.
- `X-Xeno-Routing-Candidate-Count` and `X-Xeno-Routing-Candidates`: eligible client count and IDs.
- `X-Xeno-Load-Balancing-Mode`: configured selection strategy used for the request. Supported values are `RoundRobin`, `Random`, `FirstAvailable`, `LeastRecentlyUsed`, and `Adaptive`.
- `X-Xeno-Adaptive-Sample-Count` and `X-Xeno-Adaptive-Score`: adaptive scoring evidence when `Adaptive` mode is used.
- `X-Xeno-Selection-Reason`: reason the selected node won.
- `X-Xeno-Selected-Client-Name`, `X-Xeno-Selected-API-Type`, `X-Xeno-Selected-Provider` (backward-compatible), and `X-Xeno-Selected-Source`: selected node and advertised model source metadata.
- `X-Xeno-Selected-In-Flight`, `X-Xeno-Selected-Max-Parallel`, `X-Xeno-Selected-Weight`, and `X-Xeno-Selected-Last-Heartbeat-UTC`: selected node load and health inputs.
- `X-Xeno-Client-Roundtrip-Ms` and `X-Xeno-Request-Duration-Ms`: relay timing.
- `X-Xeno-Routing-Timeline`: comma-separated routing steps.

Requests relayed from the Xeno server to a connected `xeno` client also include a forwarded requestor identity header set. The client forwards these headers to its configured provider endpoint and prints the requestor IP in `xeno connect` logs:

- `X-Forwarded-For`: original requestor IP, appended to any inbound forwarded-for chain.
- `X-Real-IP`: original requestor IP observed by Xeno.
- `X-Xeno-Requestor-IP`: original requestor IP observed by Xeno.
- `X-Xeno-Request-ID` and `X-Xeno-Tenant-ID`: Xeno request and tenant trace values.

## Request History

`GET /v1.0/api/request-history`

`GET /v1.0/api/request-history/summary`

`GET /v1.0/api/request-history/analytics/overview`

`GET /v1.0/api/request-history/{id}`

`DELETE /v1.0/api/request-history/{id}`

`POST /v1.0/api/request-history/bulk`

`GET /api/v1/requesthistory`

The request history detail response includes routing metadata, selected client metadata, request and response headers, bounded request and response bodies, telemetry fields, and timeline events. Streaming responses include stream chunk and stream event counters when the client reports them.

## Xeno Client Local Dashboard API

The Xeno Client local dashboard API is served by the background `xeno` process, not by the Xeno server. The default local dashboard base URL is `http://127.0.0.1:9343`. When `LocalDashboard.RequireLocalToken` is true, the embedded dashboard page receives a per-process token and sends it to the local API.

These routes are intended for same-machine dashboard use and local diagnostics:

`GET /health`

`GET /api/v1/state`

`GET /api/v1/statistics`

`GET /api/v1/database`

`GET /api/v1/providers`

`POST /api/v1/providers`

`PUT /api/v1/providers/{name}`

`DELETE /api/v1/providers/{name}`

`POST /api/v1/providers/{name}/test`

`GET /api/v1/models`

`POST /api/v1/models`

`PUT /api/v1/models/{provider}/{name}`

`DELETE /api/v1/models/{provider}/{name}`

`GET /api/v1/requests`

`GET /api/v1/requests/summary`

`GET /api/v1/requests/analytics/overview`

`GET /api/v1/requests/{id}`

`DELETE /api/v1/requests/{id}`

`POST /api/v1/requests/bulk`

`GET /api/v1/logs`

`GET /api/v1/settings`

`PUT /api/v1/settings`

Local provider create/update request bodies use `name`, `apiType`, `endpoint`, `timeoutMs`, optional `credential`, and `clearCredential`. Local model create/update request bodies use `provider`, `name`, `supportsCompletions`, and `supportsEmbeddings`. Local settings updates modify `LocalDashboard` settings in `~/.xeno/client.json`; listener host, listener port, token mode, and database filename changes take effect after restarting the client.

Planned local CPU, memory, disk, and GPU telemetry routes are documented in [CLIENT_TELEMETRY.md](CLIENT_TELEMETRY.md). They are not part of the current released local API.

## WebSocket

`GET /ws/v1/clients`

Upgrades to WebSocket. Clients send `client.register` and periodic `client.heartbeat` envelopes. Server-dispatched inference requests use `server.request`; clients respond with `client.response`.
Streaming requests use `client.response.start`, `client.response.chunk`, and `client.response.complete` frames so the server can relay chunks and persist final telemetry.
The server waits `WebSocket.RequestTimeoutMs` for each routed inference response before returning a `504 ClientTimeout`.
The WebSocket handshake requires `x-access-key` plus `x-secret-key`, with `x-tenant-guid`. Bearer session tokens and user passwords are rejected for client control-plane connections.

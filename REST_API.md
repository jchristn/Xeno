# Xeno REST API

Base URL: `http://localhost:9000`

The server exposes Watson 7 routes with `/openapi.json` and `/swagger`. Tenant-scoped calls accept `x-tenant-guid`; if omitted, the seeded `ten_default` tenant is used.

## System

`GET /health`

`GET /v1.0/api/health`

`GET /openapi.json`

`GET /swagger`

`GET /metrics`

## SDK Generation

No generated SDK packages are checked into the v0.1.0 alpha repository. Generate SDKs from the live OpenAPI document at `GET /openapi.json`.

## Authentication

`POST /api/v1/auth/token`

`POST /v1.0/api/auth/token`

`POST /api/v1/auth/login`

`POST /v1.0/api/auth/login`

`GET /v1.0/api/auth/token/details`

`POST /v1.0/api/auth/token/refresh`

`POST /v1.0/api/auth/logout`

Token issuance accepts direct credential fields. Bearer and `x-token` values must match when both are supplied.

Login accepts `username`, `password`, and optional `tenantId`. It returns a generated `accessKey` and `secretKey` for client use. The user password is only accepted by the login endpoint and is never accepted on the WebSocket control plane.

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

Legacy dashboard aliases under `/api/v1/...` are also registered for administration, clients, token bootstrap, settings, request history, and OpenAI-compatible routes.

## OpenAI-Compatible API

`GET /v1/models`

`POST /v1/chat/completions`

`POST /v1/completions`

`POST /v1/embeddings`

OpenAI-compatible aliases are also available under `/api/v1/openai/v1/...`.

## Request History

`GET /v1.0/api/request-history`

`GET /v1.0/api/request-history/summary`

`GET /v1.0/api/request-history/analytics/overview`

`GET /v1.0/api/request-history/{id}`

`DELETE /v1.0/api/request-history/{id}`

`POST /v1.0/api/request-history/bulk`

`GET /api/v1/requesthistory`

## WebSocket

`GET /ws/v1/clients`

Upgrades to WebSocket. Clients send `client.register` and periodic `client.heartbeat` envelopes. Server-dispatched inference requests use `server.request`; clients respond with `client.response`.
The server waits `WebSocket.RequestTimeoutMs` for each routed inference response before returning a `504 ClientTimeout`.
The WebSocket handshake requires `x-access-key` plus `x-secret-key`, with `x-tenant-guid`. Bearer session tokens and user passwords are rejected for client control-plane connections.

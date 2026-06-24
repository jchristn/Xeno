# Xeno REST API

Base URL: `http://localhost:9000`

The server exposes Watson 7 routes with `/openapi.json` and `/swagger`. Tenant-scoped calls accept `x-tenant-guid`; if omitted, the seeded `ten_default` tenant is used.

## System

`GET /health`

`GET /v1.0/api/health`

`GET /openapi.json`

`GET /swagger`

`GET /metrics`

## Authentication

`POST /api/v1/auth/token`

`POST /v1.0/api/auth/token`

`POST /api/v1/auth/login`

`POST /v1.0/api/auth/login`

`GET /v1.0/api/auth/token/details`

`POST /v1.0/api/auth/token/refresh`

`POST /v1.0/api/auth/logout`

Token issuance accepts an administrator API key or direct credential fields. Bearer and `x-token` values must match when both are supplied.

Login accepts `username`, `password`, and optional `tenantId`. It returns a generated `accessKey` and `secretKey` for client use. The user password is only accepted by the login endpoint and is never accepted on the WebSocket control plane.

## Administration

`GET /v1.0/api/tenants`

`GET /v1.0/api/users`

`GET /v1.0/api/credentials`

`POST /v1.0/api/credentials`

`GET /v1.0/api/roles`

`GET /v1.0/api/permissions`

`GET /v1.0/api/effective-permissions`

`GET /v1.0/api/audit`

`GET /v1.0/api/clients`

The clients response includes connection state, capacity fields, and each client's advertised `models` inventory.

`GET /v1.0/api/model-definitions`

`GET /v1.0/api/model-configurations`

`GET /v1.0/api/analytics/overview`

`GET /v1.0/api/settings`

`POST /v1.0/api/settings`

`GET /v1.0/api/backup`

`POST /v1.0/api/restore`

Legacy dashboard aliases under `/api/v1/...` are also registered for clients, token bootstrap, settings, and request history.

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
The WebSocket handshake requires `x-access-key` plus `x-secret-key`, or `x-admin-key`, with `x-tenant-guid`. Bearer session tokens and user passwords are rejected for client control-plane connections.

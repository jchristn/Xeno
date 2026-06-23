# Xeno REST API

Base URL: `http://localhost:9000`

The server exposes Watson 7 routes with `/openapi.json` and `/swagger`. Tenant-scoped calls accept `x-tenant-guid`; if omitted, the seeded `ten_default` tenant is used.

## System

`GET /health`

`GET /v1.0/api/health`

`GET /openapi.json`

`GET /swagger`

## Authentication

`POST /api/v1/auth/token`

`POST /v1.0/api/auth/token`

Returns a local development bearer-token payload bound to the resolved tenant.

## Administration

`GET /v1.0/api/tenants`

`GET /v1.0/api/users`

`GET /v1.0/api/credentials`

`GET /v1.0/api/clients`

`GET /v1.0/api/model-runner-endpoints`

`GET /v1.0/api/model-definitions`

`GET /v1.0/api/model-configurations`

`GET /v1.0/api/load-balancing-policies`

`GET /v1.0/api/model-access-policies`

`GET /v1.0/api/virtual-model-runners`

`GET /v1.0/api/virtual-model-runner-reservations`

`POST /v1.0/api/virtual-model-runners/explain-routing`

Legacy dashboard aliases under `/api/v1/...` are also registered for clients, model/routing lists, token bootstrap, and request history.

## OpenAI-Compatible API

`GET /v1/models`

`POST /v1/chat/completions`

`POST /v1/completions`

`POST /v1/embeddings`

OpenAI-compatible aliases are also available under `/api/v1/openai/v1/...`.

## Request History

`GET /v1.0/api/request-history`

`GET /v1.0/api/request-history/summary`

`DELETE /v1.0/api/request-history/{id}`

`GET /api/v1/requesthistory`

## WebSocket

`GET /ws/v1/clients`

Upgrades to WebSocket. Clients send `client.register` and periodic `client.heartbeat` envelopes. Server-dispatched inference requests use `server.request`; clients respond with `client.response`.
The server waits `WebSocket.RequestTimeoutMs` for each routed inference response before returning a `504 ClientTimeout`.

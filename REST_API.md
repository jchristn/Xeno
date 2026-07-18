# Xeno REST API

Base URL: `http://localhost:9000`

The server exposes Watson 7 routes with `/openapi.json` and `/swagger`. Protected REST routes use bearer auth. The bearer value may be an issued session token or a globally unique tenant credential value such as the seeded `default`; the authenticated session or credential establishes tenant context. Tenant-scoped calls may still accept `x-tenant-guid` as an optional selector or compatibility hint, and supplied tenant hints must match the authenticated bearer tenant. Tenant headers do not authenticate a caller. Matching `x-token` and legacy direct `x-access-key` credentials remain accepted for compatibility. Swagger UI includes a Xeno authentication panel that loads saved tenant, bearer token, `x-token`, and access key material into Try it out requests.

The REST API is the operator and application surface for a federated accelerator fleet. Applications use the OpenAI-compatible routes so they can call one endpoint regardless of which connected machine ultimately serves the request. Operators use the management routes to inspect clients, advertised models, telemetry, request history, routing policy, tenants, credentials, and settings for the controller.

## System

`GET /health`

`GET /v1.0/api/health`

`GET /v1.0/api/ping`

`GET /openapi.json`

`GET /swagger`

`GET /metrics`

`GET /v1.0/api/ping` is an unauthenticated real-time heartbeat endpoint for CLI and operational checks. Optional `sequence` and `clientSentUtc` query values are echoed in the JSON response alongside `status`, `product`, `version`, and `serverUtc`.

## SDKs

C#, JavaScript, and Python SDKs are checked in under `src/sdk/`. Each SDK includes its own `README.md`, `TESTING.md`, automated tests, and an interactive console harness.

The live OpenAPI document remains available at `GET /openapi.json` and should be treated as the contract source of truth when validating or regenerating SDK coverage.

## Ledger

Ledger routes are authenticated and tenant-fenced. They require NetLedger v3.0.0 or later when ledgering is enabled because Xeno stores tenant-scoped account and entry correlation through NetLedger labels and tags.

- `GET /v1.0/api/ledger/status`
- `GET /v1.0/api/ledger/balance/me`
- `GET /v1.0/api/ledger/balances`
- `GET /v1.0/api/ledger/balances/users/{userId}`
- `GET /v1.0/api/ledger/entries/me`
- `POST /v1.0/api/ledger/entries/search`
- `POST /v1.0/api/ledger/drain`
- `POST /v1.0/api/ledger/reconcile`

Ledgering records successful completion and embedding requests with known `total_tokens`. Xeno debits the requesting user's account and credits the owner of the selected serving client by the same raw token amount. No enforcement, pricing, reservations, or quotas are active in this release.

System administrators can read global balances and entries. Tenant administrators can read tenant-scoped balances and entries. Regular users can read only their own balance and entries. Admin drain and reconcile calls append audit records.

Tenant creation and onboarding can include `deferSubordinateProvisioning: true` to report NetLedger provisioning status instead of failing when NetLedger is unavailable. Without that flag, new tenant provisioning fails if the required NetLedger tenant/account setup cannot complete. Tenant update, suspension, reactivation, and deletion paths update subordinate service status on the tenant response; hard delete removes the NetLedger tenant before clearing Xeno ledger links.

Entry search accepts filters such as `tenantId`, `userId`, `direction`, `selectedClientId`, `requestCredentialId`, `requestHistoryId`, `fromUtc`, `toUtc`, `continuationToken`, and `pageSize`.

## Client Telemetry

All client telemetry routes are tenant-scoped and require authentication. Canonical routes use `/v1.0/api/...`.
Server-collected telemetry is stored in the database engine selected by the server JSON `Database` settings. SQLite, PostgreSQL, SQL Server, and MySQL all support host telemetry, provider telemetry, collection attempts, details, deletes, rollups, and tenant purge cleanup.

- `GET /v1.0/api/client-telemetry/status`
- `GET /v1.0/api/client-telemetry/latest`
- `POST /v1.0/api/client-telemetry/collect`
- `POST /v1.0/api/client-telemetry/history/search`
- `POST /v1.0/api/client-telemetry/history/rollups`
- `GET /v1.0/api/client-telemetry/history/{id}`
- `DELETE /v1.0/api/client-telemetry/history/{id}`
- `POST /v1.0/api/client-telemetry/history/delete`
- `GET /v1.0/api/client-telemetry/providers/latest`
- `POST /v1.0/api/client-telemetry/providers/collect`
- `POST /v1.0/api/client-telemetry/providers/history/search`
- `POST /v1.0/api/client-telemetry/providers/history/rollups`
- `GET /v1.0/api/client-telemetry/providers/history/{id}`
- `DELETE /v1.0/api/client-telemetry/providers/history/{id}`
- `POST /v1.0/api/client-telemetry/providers/history/delete`
- `GET /v1.0/api/client-telemetry/collection-attempts`
- `GET /v1.0/api/client-telemetry/metrics`

`status` returns telemetry settings state, service running state, last/next collection timestamps, connected client count, eligible client count, and recent failure count. `latest` returns `{ hostTelemetry, providerTelemetry }`, where each property is an array of the most recent persisted samples. `collect` triggers immediate server-requested collection for all connected clients or for a supplied `clientId`. Search endpoints accept `clientId`, `metric`, `fromUtc`, `toUtc`, `limit`, and `offset`; they return `{ total, items }`. Rollup endpoints accept `clientId`, `metric`, `fromUtc`, `toUtc`, and `bucketMinutes`; they return an array of buckets shaped as `{ startUtc, endUtc, label, count, average, minimum, maximum }`. Host telemetry `metric` values use enum names such as `CpuUtilizationPercent`, `MemoryUtilizationPercent`, and `GpuUtilizationPercent`. Detail routes return normalized rows and bounded raw snapshot JSON when persisted. Delete routes are tenant-scoped.

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

Token issuance accepts direct credential fields. REST clients may also send a globally unique tenant credential value directly as `Authorization: Bearer <credential>` without a tenant header. Bearer and `x-token` values must match when both are supplied.

Login accepts `username`, `password`, and optional `tenantId`. It returns a generated `accessKey` for client use. For REST calls, send that credential value as a bearer token. The user password is only accepted by the login endpoint and is never accepted on the WebSocket control plane.

Dashboard login is a multistep flow. The dashboard first resolves the server URL and user email, retrieves matching tenants, prompts for tenant only when more than one tenant is available, and then submits the password.

Tenant membership is one tenant per user record. The same email address may
exist as separate user rows in multiple tenants; non-system-admin users switch
tenants by logging out and logging back in to the desired tenant.

Protected REST calls return 401 when authentication is missing or invalid and
403 when the caller is authenticated but lacks authority. System admins can
administer all tenants. Tenant admins can administer only their own tenant and
cannot change `isAdmin`. RBAC users and credentials can use only permissions
assigned through active roles in their own tenant.

Password login, dashboard login, and token creation are rate limited by source
IP. The setting is `Auth.MaxLoginRequestsPerMinute` in the server JSON settings
file. When the limit is exceeded the server returns 429 with `RateLimited`.

## Onboarding

`POST /api/v1/onboarding/signup`

`POST /v1.0/api/onboarding/signup`

The onboarding signup route is unauthenticated. It supports both initial tenant
creation and adding a user to an existing tenant.

- Omit `tenantId` to create a new tenant and onboard the first user as that tenant's tenant administrator. In this mode `tenantName`, `email`, and `password` are required.
- Include `tenantId` to add the user to an existing active tenant as a regular user. In this mode `tenantName` is not required.
- `username` is optional and defaults to `email`.
- `accessKey` is not accepted in the request. The server generates credential material and returns the plaintext secret key only in the onboarding response.
- Validation failures return `400 BadRequest`. The email address must be syntactically valid, password must be at least 8 characters, tenant IDs must be 64 characters or fewer, and tenant names and usernames must be 256 characters or fewer.
- A supplied `tenantId` that does not resolve returns `404 TenantNotFound`.
- An inactive tenant, duplicate email address, duplicate username in the target tenant, or an email address already held by a global administrator returns `409 Conflict` with `TenantInactive`, `EmailAlreadyExists`, `UsernameAlreadyExists`, or `GlobalAdminEmailAlreadyExists`.
- Onboarding is rate limited with the same source-IP limiter as login. Rate-limited requests return `429 RateLimited`.
- Successful responses include a `request` object with non-secret metadata for drill-down and telemetry: request ID, optional correlation ID, method, path, source IP, forwarded-for, user agent, origin, referer, host, and event timestamp. The same metadata is persisted on audit records.

New tenant request:

```json
{
  "tenantName": "Example Company",
  "email": "owner@example.com",
  "username": "owner@example.com",
  "password": "password"
}
```

Existing tenant request:

```json
{
  "tenantId": "ten_abc123",
  "email": "user@example.com",
  "username": "user@example.com",
  "password": "password"
}
```

Response:

```json
{
  "status": "success",
  "tenant": {
    "id": "ten_abc123",
    "name": "Example Company",
    "active": true,
    "createdUtc": "2026-07-08T16:00:00Z",
    "lastUpdateUtc": "2026-07-08T16:00:00Z"
  },
  "user": {
    "id": "usr_abc123",
    "tenantId": "ten_abc123",
    "username": "owner@example.com",
    "email": "owner@example.com",
    "isAdmin": false,
    "isTenantAdmin": true,
    "active": true
  },
  "credential": {
    "id": "cred_abc123",
    "tenantId": "ten_abc123",
    "userId": "usr_abc123",
    "accessKey": "xeno_abc123",
    "secretKey": "xeno_secret_value",
    "active": true
  },
  "token": {
    "token": "session-token-value",
    "tokenType": "Bearer",
    "tenantId": "ten_abc123",
    "userId": "usr_abc123",
    "sessionId": "sess_abc123",
    "expiresUtc": "2026-07-08T17:00:00Z"
  },
  "request": {
    "requestId": "req_abc123",
    "correlationId": "corr_abc123",
    "method": "POST",
    "path": "/v1.0/api/onboarding/signup",
    "sourceIp": "203.0.113.10",
    "forwardedFor": "203.0.113.10, 198.51.100.7",
    "userAgent": "ExampleClient/1.0",
    "origin": "https://app.xenocloud.ai",
    "referer": "https://app.xenocloud.ai/signup",
    "host": "api.xenocloud.ai",
    "eventUtc": "2026-07-08T16:00:00Z"
  }
}
```

Validation error:

```json
{
  "error": "BadRequest",
  "message": "Email address is invalid."
}
```

Duplicate email error:

```json
{
  "error": "EmailAlreadyExists",
  "message": "Email already exists in the tenant."
}
```

Global administrator email conflict:

```json
{
  "error": "GlobalAdminEmailAlreadyExists",
  "message": "Email already belongs to a global administrator."
}
```

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

`POST /v1.0/api/admin/nuke/tenant`

`POST /v1.0/api/admin/nuke/user`

`GET /v1.0/api/roles`

`POST /v1.0/api/roles`

`PUT /v1.0/api/roles/{id}`

`DELETE /v1.0/api/roles/{id}`

`GET /v1.0/api/permissions`

`GET /v1.0/api/permissions/grantable`

`POST /v1.0/api/permissions`

`PUT /v1.0/api/permissions/{id}`

`DELETE /v1.0/api/permissions/{id}`

`GET /v1.0/api/effective-permissions`

`GET /v1.0/api/role-assignments`

`POST /v1.0/api/role-assignments`

`DELETE /v1.0/api/role-assignments/{id}`

`GET /v1.0/api/audit`

`DELETE /v1.0/api/audit/{id}`

`GET /v1.0/api/clients`

The clients response includes connection state, capacity fields, authenticated owner context, each client's advertised `models` inventory, and advertised `providers`. Provider entries include `name`, `apiType`, `modelManagementAllowed`, and `lastAdvertisedUtc`. Model entries include `modelManagementAllowed` when the backing provider is an Ollama provider that permits server-initiated model management. Owner fields are set when a WebSocket client registers: `ownerUserId`, `ownerCredentialId`, and `ownerPrincipalType`.

`DELETE /v1.0/api/clients/{id}`

`POST /v1.0/api/client-models/pull`

Pulls an Ollama model on a connected client that advertised an Ollama provider with `modelManagementAllowed: true`.

Request:

```json
{
  "clientId": "xcl_example",
  "model": "gemma3:4b",
  "source": "local-ollama",
  "insecure": false,
  "stream": true
}
```

If `stream` is `true` or the request sends `Accept: application/x-ndjson`, the response is line-delimited JSON progress events with fields such as `eventType`, `status`, `message`, `completed`, `total`, `digest`, and final `result`. Without streaming, the response is the final `client.modelManagement.response` shape.

`DELETE /v1.0/api/client-models`

Deletes an Ollama model by sending a delete command to a connected client that advertised an Ollama provider with model management enabled. The request body uses `clientId`, `name`, and optional `source`. The response is the final `client.modelManagement.response` shape.

`GET /v1.0/api/model-definitions`

`GET /v1.0/api/model-configurations`

`GET /v1.0/api/routing-policies`

`POST /v1.0/api/routing-policies`

`PUT /v1.0/api/routing-policies/{id}`

`DELETE /v1.0/api/routing-policies/{id}`

`GET /v1.0/api/analytics/overview`

`GET /v1.0/api/settings`

`POST /v1.0/api/settings`

Both settings operations use the same body shape: the raw `XenoSettings` JSON
object. `GET` returns the settings object directly. `POST` accepts the settings
object directly and returns the saved settings object directly. The response is
not wrapped in `settings`, `settingsFile`, or `restartRequired` metadata.

The server settings payload includes an `Email` section for administrative
notification workflows. `Email.Provider` supports `Disabled`, `File`, and
`Mailgun`. `Disabled` is the default. `File` writes rendered `.json`, `.html`,
and `.txt` messages to `Email.File.Directory` for local development and tests.
`Mailgun` uses `Email.Mailgun.Domain` and `Email.Mailgun.ApiKey` through the
provider abstraction in `Xeno.Email`.

Onboarding can trigger email side effects when email is enabled: platform
administrator alerts for tenant/user onboarding, tenant administrator alerts
when users join an organization, and welcome messages for new tenant
administrators or members when the corresponding notification toggles are on.
Notification failures are best-effort operational failures and do not roll back
tenant or user creation. Email templates never include passwords, bearer tokens,
access keys, secret keys, or hashed secret material. Administrator onboarding
notifications include bounded request metadata such as source IP, forwarded-for,
user agent, origin, referer, host, request path, request ID, and correlation ID
for operator drill-down.

`POST /v1.0/api/email/test`

Sends an administrative test email through the configured server email provider.
This route is only available to global/system administrators. The supplied body
is rendered through the same standard Xeno email layout used by notification
templates, including the header logo/title and footer links.

Request:

```json
{
  "toEmail": "joel.christner@gmail.com",
  "toName": "Joel Christner",
  "title": "Xeno Test Email",
  "subject": "Xeno test email",
  "body": "hello world"
}
```

Response:

```json
{
  "accepted": true,
  "skipped": false,
  "provider": "Mailgun",
  "providerMessageId": "provider-message-id",
  "statusMessage": "Email accepted by Mailgun.",
  "templateId": "administrative-test",
  "toEmail": "joel.christner@gmail.com",
  "subject": "Xeno test email",
  "sentUtc": "2026-07-10T00:00:00Z"
}
```

`GET /v1.0/api/backup`

`POST /v1.0/api/restore`

Create and update request bodies:

- Tenant create/update: `name`, `active`. Create may also include `id`; if omitted, the server generates a compact PrettyId with `ten_` prefix.
- User create/update: `username`, `email`, `isAdmin`, `isTenantAdmin`, `active`, and `password`. Password is required on create and optional on update. User responses are redacted and do not expose password material.
- Credential create/update: `userId`, `accessKey`, and `active`. Create generates an access key when omitted. Secret keys are not accepted for credential authentication or credential updates.
- Role create/update: `name`, `permissionIds`, and `active`.
- Permission create/update: `resourceType`, `operationType` (`Read`, `Write`, `Delete`, `Admin`, `Execute`), `permit`, and `active`.
- Role assignment create: `tenantId`, `roleId`, `principalType` (`User` or `Credential`), `principalId`, and `active`.
- Routing policy create/update: `name`, `modelName`, `clientName`, `requestTimeoutMs`, `loadBalancingMode`, `adaptive`, and `active`. Use `*` for `modelName` or `clientName` to match all values. Each tenant has an undeletable default `*/*` policy using `LeastRecentlyUsed`.

Admin flags are privileged security fields. Only system admins can change
`isAdmin`. System admins and same-tenant tenant admins can change
`isTenantAdmin`. Role, permission, and role-assignment writes enforce a grantor
ceiling: the caller cannot grant a permission tuple they do not already possess,
except system admins and same-tenant tenant admins inside their authority.
Seeded built-in roles and permissions are read-only through the management API.
`GET /v1.0/api/permissions/grantable` returns the permission items the caller
can assign to roles or principals.

### Administrative Nuke APIs

Administrative nuke APIs are authenticated destructive endpoints. Dashboard
flows execute only after exact-ID entry, an administrative reason, and a final
confirmation modal. API callers can send `dryRun:true` when they need an
affected-record preview, but the documented default workflow is direct execution
with `dryRun:false`.

Tenant nuke:

- `POST /v1.0/api/admin/nuke/tenant`
- Requires a global/system administrator.
- Tenant administrators always receive `403 SystemAdminRequired`.
- The default/system tenant is protected and returns `409 ProtectedTenant`.
- Deletes the tenant and tenant-owned users, credentials, sessions, roles,
  permissions, role assignments, audit records when requested, clients, model
  definitions, model configurations, routing policies, request history when
  requested, provider telemetry when requested, and client connection remnants.

Tenant nuke request:

```json
{
  "tenantId": "ten_customer",
  "confirmTenantId": "ten_customer",
  "dryRun": false,
  "reason": "Customer requested complete tenant removal.",
  "includeAuditRecords": true,
  "includeRequestHistory": true,
  "includeTelemetry": true
}
```

User nuke:

- `POST /v1.0/api/admin/nuke/user`
- Requires a global/system administrator or a tenant administrator in the target
  tenant.
- Tenant administrators may nuke any non-self user in their tenant, including
  other tenant administrators.
- Tenant administrators cannot nuke themselves, cannot nuke users in another
  tenant, and cannot nuke global/system administrators.
- Deletes the user, owned credentials, auth sessions, user and credential role
  assignments, and optionally user-related audit records. Request history user,
  credential, and session references are anonymized by default.

User nuke request:

```json
{
  "tenantId": "ten_customer",
  "userId": "usr_departed",
  "confirmUserId": "usr_departed",
  "dryRun": false,
  "reason": "User left the customer organization.",
  "includeAuditRecords": false,
  "anonymizeRequestHistory": true
}
```

Nuke response:

```json
{
  "status": "success",
  "operationId": "nuke_abc123",
  "operationType": "UserNuke",
  "dryRun": false,
  "tenant": {
    "tenantId": "ten_customer",
    "name": "Customer",
    "status": "Active"
  },
  "user": {
    "userId": "usr_departed",
    "email": "departed@example.com",
    "status": "Active"
  },
  "requestedBy": {
    "tenantId": "ten_customer",
    "userId": "usr_admin",
    "isGlobalAdmin": false,
    "isTenantAdmin": true
  },
  "deleted": {
    "tenants": 0,
    "users": 1,
    "credentials": 2,
    "authSessions": 3,
    "roles": 0,
    "permissions": 0,
    "roleAssignments": 2,
    "auditRecords": 0,
    "clients": 0,
    "modelDefinitions": 0,
    "modelConfigurations": 0,
    "routingPolicies": 0,
    "requestHistory": 0,
    "telemetryRecords": 0
  },
  "updated": {
    "requestHistoryAnonymized": 12
  },
  "warnings": [],
  "startedUtc": "2026-07-08T18:17:10.012Z",
  "completedUtc": "2026-07-08T18:17:10.103Z"
}
```

Nuke errors:

- `400 BadRequest`: missing IDs, confirmation mismatch, or invalid reason.
- `401 Unauthorized`: missing or invalid authentication.
- `403 SystemAdminRequired`: tenant nuke attempted by a non-system admin.
- `403 TenantAdminRequired`: user nuke attempted without same-tenant tenant
  admin or system admin authority.
- `403 SystemAdminProtected`: tenant admin attempted to nuke a global admin.
- `404 TenantNotFound` or `404 UserNotFound`: target object does not exist.
- `409 ProtectedTenant`: target tenant is protected.
- `409 TenantAdminSelfNukeProtected`: tenant admin attempted to nuke itself.
- `409 LastSystemAdminProtected`: target is the last active global admin.

Legacy dashboard aliases under `/api/v1/...` are also registered for administration, clients, token bootstrap, settings, request history, and OpenAI-compatible routes.

## OpenAI-Compatible API

`GET /v1/models`

Returns an OpenAI-compatible model list:

```json
{
  "object": "list",
  "data": [
    {
      "id": "llama3.2:latest",
      "object": "model",
      "created": 1762300800,
      "owned_by": "xeno"
    }
  ]
}
```

`POST /v1/chat/completions`

`POST /v1/completions`

`POST /v1/embeddings`

OpenAI-compatible aliases are also available under `/api/v1/openai/v1/...`.

OpenAI-compatible routes are protected application APIs. `GET /v1/models`
requires `Inference.Read`. `POST /v1/chat/completions`,
`POST /v1/completions`, and `POST /v1/embeddings` require
`Inference.Execute`. Authentication should use `Authorization: Bearer ...`,
where the bearer value is an issued session token or a globally unique tenant
credential value. No tenant header is required for bearer credentials; the
server infers the tenant from the authenticated session or credential. Matching
`x-token` and legacy `x-access-key` remain accepted; any supplied tenant header
must match the authenticated session or credential tenant unless the caller is a
system admin. Anonymous requests receive 401 and cannot dispatch to a connected
client.

Routed OpenAI-compatible responses include only `X-Xeno-Request-ID` as a
caller-visible Xeno trace header. The request ID is also the persisted request
history entry ID for new routed requests. Detailed routing diagnostics such as
selected client, candidate clients, excluded clients, routing policy, selection
reason, timing, token telemetry, and timeline are captured in Request History
and surfaced through the dashboard and protected Request History APIs instead
of HTTP response headers.

For streamed completion and chat requests, the selected `xeno` client requests
provider usage chunks with `stream_options.include_usage = true` for Ollama,
OpenAI, vLLM, and generic OpenAI-compatible endpoints. Request history stores
OpenAI `usage` token fields and normalizes Ollama-native `prompt_eval_count`
and `eval_count` into prompt, completion, and total token telemetry.

## Ollama-Compatible API

`GET /api/tags`

Returns an Ollama-compatible model list:

```json
{
  "models": [
    {
      "name": "llama3.2:latest",
      "model": "llama3.2:latest",
      "modified_at": "2026-07-06T00:00:00.0000000Z",
      "size": 0,
      "digest": "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
      "details": {
        "format": "",
        "family": "llama3.2",
        "families": [
          "llama3.2"
        ],
        "parameter_size": "",
        "quantization_level": ""
      }
    }
  ]
}
```

`POST /api/chat`

`POST /api/generate`

`POST /api/embed`

`POST /api/embeddings`

Ollama-compatible routes are protected application APIs. `GET /api/tags`
requires `Inference.Read`. Generation and embedding requests require
`Inference.Execute`. These routes are relayed to connected Ollama providers
using the native Ollama path, and successful responses preserve the provider's
Ollama response body shape. Routed responses include `X-Xeno-Request-ID` for
correlation with Request History.

Requests relayed from the Xeno server to a connected `xeno` client also include a forwarded requestor identity header set. The client forwards these headers to its configured provider endpoint and prints the requestor IP in `xeno connect` logs:

- `X-Forwarded-For`: original requestor IP, appended to any inbound forwarded-for chain.
- `X-Real-IP`: original requestor IP observed by Xeno.
- `X-Xeno-Requestor-IP`: original requestor IP observed by Xeno.
- `X-Xeno-Request-ID` and `X-Xeno-Tenant-ID`: Xeno request and tenant trace values.

## Request History

`GET /v1.0/api/request-history`

Returns a paged result object:

```json
{
  "items": [],
  "total": 0,
  "page": 1,
  "pageSize": 100
}
```

Supported query parameters are `page` (default `1`), `pageSize` (default `100`, maximum `1000`), `fromUtc`, and `toUtc`. Use `total` for dashboard counters and pagination instead of enumerating every request-history entry.

`GET /v1.0/api/request-history/model-leaderboard`

`POST /v1.0/api/request-history/model-leaderboard`

Returns a public, unauthenticated leaderboard of model token usage and request
counts aggregated from request-history entries. When `tenantId` is omitted, the
leaderboard is global across all tenants. Include `tenantId` only when
tenant-scoped results are required. The GET route accepts query-string
parameters; the POST route accepts the same fields as JSON:

```json
{
  "fromUtc": "2026-07-01T00:00:00Z",
  "toUtc": "2026-07-08T00:00:00Z",
  "limit": 10,
  "offset": 0
}
```

Tenant-scoped request:

```json
{
  "tenantId": "ten_default",
  "fromUtc": "2026-07-01T00:00:00Z",
  "toUtc": "2026-07-08T00:00:00Z",
  "limit": 10,
  "offset": 0
}
```

The response is rank ordered by total tokens descending. `total` is the number
of matching model records before `limit` and `offset` are applied.
`limit` and `offset` are optional integer fields. Omit them or send JSON
`null` to use defaults (`limit` = `100`, `offset` = `0`).

```json
{
  "models": {
    "llama3.2:latest": {
      "requestCount": 42,
      "totalTokens": 124000,
      "promptTokens": 78000,
      "outputTokens": 46000
    }
  },
  "total": 1,
  "limit": 10,
  "offset": 0
}
```

`GET /v1.0/api/request-history/summary`

`GET /v1.0/api/request-history/analytics/overview`

Analytics overview accepts optional `fromUtc` and `toUtc` parameters and returns request totals plus aggregate groups such as `byModel`, `byPath`, and `byClient`, along with client/model KPI counts.

`GET /v1.0/api/request-history/{id}`

`DELETE /v1.0/api/request-history/{id}`

`POST /v1.0/api/request-history/bulk`

`GET /api/v1/requesthistory`

The request history detail response includes routing metadata, selected client metadata, resolved requestor tenant/principal fields, request and response headers, bounded request and response bodies, telemetry fields, and timeline events. Streaming responses include stream chunk and stream event counters when the client reports them.

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

`GET /api/v1/telemetry/status`

`GET /api/v1/telemetry/latest`

`POST /api/v1/telemetry/collect`

`POST /api/v1/telemetry/history/search`

`POST /api/v1/telemetry/history/rollups`

`GET /api/v1/telemetry/history/{id}`

`DELETE /api/v1/telemetry/history/{id}`

`POST /api/v1/telemetry/history/delete`

`GET /api/v1/telemetry/providers/status`

`GET /api/v1/telemetry/providers/latest`

`POST /api/v1/telemetry/providers/collect`

`POST /api/v1/telemetry/providers/history/search`

`POST /api/v1/telemetry/providers/history/rollups`

`GET /api/v1/telemetry/providers/history/{id}`

`DELETE /api/v1/telemetry/providers/history/{id}`

`POST /api/v1/telemetry/providers/history/delete`

`GET /api/v1/logs`

`GET /api/v1/settings`

`POST /api/v1/settings/reload`

`PUT /api/v1/settings`

Local provider create/update request bodies use `name`, `apiType`, `endpoint`, `timeoutMs`, optional `credential`, `clearCredential`, and `allowModelManagement`. `allowModelManagement` is valid only for Ollama providers and permits the Xeno server to issue pull/delete model commands through the client WebSocket. Local model create/update request bodies use `provider`, `name`, `supportsCompletions`, and `supportsEmbeddings`. Provider and model mutations persist to `~/.xeno/client.json`, refresh local provider/model snapshots, and request `client.inventory.update` for an already connected server session. `POST /api/v1/settings/reload` re-reads `~/.xeno/client.json` after manual file edits and requests the same inventory update. Local telemetry `latest` returns `{ snapshot, detail, status }`. Local telemetry search bodies use `metric`, `fromUtc`, `toUtc`, `limit`, `offset`, and optional GPU filters and return `{ total, samples }`. Local telemetry rollup bodies use `metric`, `fromUtc`, `toUtc`, `bucketMinutes`, and optional GPU filters and return an array of `{ startUtc, endUtc, label, count, average, minimum, maximum }` buckets. Local host telemetry `metric` values use enum names such as `CpuUtilizationPercent`, `MemoryUtilizationPercent`, and `GpuUtilizationPercent`. Local provider telemetry search and rollup bodies use `metric`, `providerName`, `modelName`, `fromUtc`, `toUtc`, `bucketMinutes`, `limit`, and `offset`; search returns `{ total, samples }` and rollups return the same bucket array shape. Local settings updates modify `LocalDashboard` and `Telemetry` settings in `~/.xeno/client.json`; listener host, listener port, token mode, database filename, host telemetry scheduler, and provider telemetry scheduler changes take effect after restarting the client.

## WebSocket

`GET /ws/v1/clients`

Upgrades to WebSocket. Clients send `client.register` and periodic `client.heartbeat` envelopes. Server-dispatched inference requests use `server.request`; clients respond with `client.response`.
Streaming requests use `client.response.start`, `client.response.chunk`, and `client.response.complete` frames so the server can relay chunks and persist final telemetry.
The server waits `WebSocket.RequestTimeoutMs` for each routed inference response before returning a `504 ClientTimeout`.
The WebSocket handshake requires `x-access-key` with `x-tenant-guid`. Bearer session tokens, user passwords, and `x-secret-key` are rejected for client control-plane connections.

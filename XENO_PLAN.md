# Xeno Build Plan

Xeno is an AI control plane with three first-release surfaces:

- a Watson 7 server that exposes OpenAI-compatible inference routes, a documented REST management API, and a WebSocket control plane for connected clients;
- a cross-platform `xeno` console client that advertises local hardware and model runner capacity, keeps a persistent server connection, and executes routed inference jobs;
- a React/Vite dashboard for operating tenants, users, credentials, clients, models, routing, reservations, request history, and diagnostics.

The plan below is written as an implementation contract. Any code that diverges from the requirements in `C:\code\agents\requirements` or from Conductor's routing controls should be treated as incomplete, even if it works for the happy path.

## Live Implementation Status

Last updated: 2026-06-24.

The tracker below is the current source of truth for implementation progress. When a phase moves forward, update this section in the same commit as the code change.

| Phase | Status | Current evidence | Remaining work |
| --- | --- | --- | --- |
| 1. Repository and project scaffolding | Complete | Root files, solution, server/client/core/test projects, dashboard, Docker assets, scripts, MIT license, Postman collection. | Keep docs and Postman synchronized with new routes. |
| 2. Core models, settings, serialization, and IDs | In progress | Core settings/models, enum serialization, PrettyId helpers, one-class-per-file structure. | Add full security, authorization, protocol envelope, analytics, and validation DTO coverage. |
| 3. Database providers and migrations | In progress | Provider-specific drivers for SQLite, PostgreSQL, MySQL, and SQL Server initialize durable state storage, record `schema_migrations` version `0001`, and SQLite persistence is tested. | Replace compact platform-state persistence with normalized per-entity tables, provider query classes/method interfaces, and provider integration tests against live PostgreSQL/MySQL/SQL Server. |
| 4. Authentication, RBAC, sessions, audit | In progress | Session tokens, direct credential auth, admin API key auth, built-in roles/permissions, effective permissions, credential creation, audit records, and Touchstone coverage exist. | Add signed request replay protection, immutable built-in role write guards, fuller permission assignment APIs, route-level denial tests, and encrypted/hashed credential secret storage. |
| 5. Watson REST host and OpenAPI | In progress | Watson 7 host, CORS preflight/post-routing, Swagger/OpenAPI, health, authentication, authorization, audit, management lists/actions, policy evaluation, backup/restore, metrics, and OpenAI routes are available. | Split route registrars, expand typed DTOs, complete every CRUD/action route, and attach full request context metadata through Watson metadata. |
| 6. WebSocket gateway and client protocol | In progress | Clients register/heartbeat, advertise inventory, receive jobs, respond, disconnect cleanup; routed OpenAI completion relay is tested. | Add typed envelopes, handshake auth, stale heartbeat timeout, streaming chunks, cancellation, protocol versioning, and tenant-conflict rejection tests. |
| 7. Xeno client CLI and provider adapters | In progress | `xeno` tool packaging, config editing, persistent reconnect loop, local HTTP forwarding to configured runners, basic OS/CPU/memory inventory, WebSocket auth headers, upstream bearer forwarding, and runner connectivity tests exist. | Add GPU/model hardware probes, streaming relay, job cancellation handling, OS-protected secret storage, and service install/uninstall. |
| 8. Conductor-compatible routing controls | In progress | VMRs, endpoints, model definitions/configurations, route explanation, connected-client eligibility, least-in-flight policy selection, model access enforcement, active reservation gates, effective VMR views, policy validation/evaluation, and model-load metadata updates have Touchstone coverage. | Add full metric catalog expressions, session affinity persistence, model mutation/config mappings, real provider load-model dispatch, and richer explain-routing evidence. |
| 9. Request history, analytics, metrics, backup/restore | In progress | Routed request history, summary buckets, request-history detail/bulk routes, analytics overview, Prometheus-style metrics, and platform backup/restore exist. | Add redaction/truncation details, retention cleanup service, advanced search/pagination filters, TTFT/token/cost analytics, and restore validation for tenant references/reservation overlaps. |
| 10. Dashboard | In progress | React/Vite dashboard now includes i18n runtime, Home, Request History with details/bulk clear, API Explorer, Security, Clients, VMRs with load/explain controls, Policies, Reservations, Analytics, Settings backup/restore, and Docker/nginx packaging. | Add create/edit forms for every entity, permission-aware disabled states, dashboard automated tests, and responsive visual QA screenshots. |
| 11. Test matrix and release hardening | In progress | `dotnet build` passes with 0 warnings/errors; Touchstone console/xUnit/NUnit suites pass at 19 tests; dashboard audit/build pass; Docker compose config passes; server/dashboard Docker images build; no vulnerable NuGet packages; Postman/settings JSON validate. | Add provider live integration tests, full Docker compose runtime smoke, dashboard automated tests, streaming/cancel/disconnect tests, and visual responsive QA. |

## Non-Blocking Assumptions

No blocking questions were needed before drafting this plan. These decisions should still be confirmed during implementation kickoff:

- Use `net10.0` for all C# projects, matching the current Conductor build style, with every `.csproj` version set to `0.1.0`.
- Use `http://localhost:9000` for the server and `http://localhost:9100` for the dashboard in local Docker.
- Use PostgreSQL as the main Docker compose database profile, while keeping SQLite available for the smallest local boot path and adding MySQL and SQL Server compose profiles on day one.
- Expose OpenAI-compatible protocol routes at `/v1/...` for client compatibility and mirror them under `/api/v1/openai/...` where useful for OpenAPI discoverability. All management/control-plane REST routes use `/api/v1/...`.
- Keep Conductor terminology where it represents a feature exactly: Virtual Model Runner, Model Runner Endpoint, Model Definition, Model Configuration, Load-Balancing Policy, Model Access Policy, Reservation, Routing Decision, and Request History.

## Repository Contract

The root must be created first so later work lands in stable locations.

```text
/
|-- README.md
|-- CHANGELOG.md
|-- REST_API.md
|-- LICENSE.md
|-- LICENSE
|-- Xeno.postman_collection.json
|-- AGENTS.md
|-- XENO_PLAN.md
|-- xeno.json
|-- .gitignore
|-- .dockerignore
|-- build-all.bat
|-- build-server.bat
|-- build-dashboard.bat
|-- src/
|   |-- Xeno.sln
|   |-- Xeno.Core/
|   |-- Xeno.Server/
|   |-- Xeno.Client/
|   |-- Test.Shared/
|   |-- Test.Automated/
|   |-- Test.Xunit/
|   |-- Test.Nunit/
|-- dashboard/
|-- docker/
|   |-- compose.yaml
|   |-- xeno.json
|   |-- postgres/
|   |-- mysql/
|   |-- sqlserver/
|   |-- data/
|   |-- logs/
|   |-- factory/
|       |-- reset.bat
|       |-- reset.sh
|       |-- xeno.json
|       |-- schema.sql
```

`LICENSE.md` is required by the repository requirements. `LICENSE` should duplicate the same MIT text because the requested root shape names `LICENSE`. `xeno.json` is the default server settings file required by the backend architecture guidance; `docker/xeno.json` is the deployment-specific copy. `REST_API.md` is the canonical API reference and should be generated or refreshed from the OpenAPI surface before release.

Root scripts:

- `build-server.bat` builds `jchristn77/xeno-server:v0.1.0` and `jchristn77/xeno-server:latest` from `src/Xeno.Server/Dockerfile`.
- `build-dashboard.bat` builds `jchristn77/xeno-dashboard:v0.1.0` and `jchristn77/xeno-dashboard:latest` from `dashboard/Dockerfile`.
- `build-all.bat` builds server and dashboard images, then runs solution and dashboard tests.

Docker files must use `.yaml`, not `.yml`. Compose may include `build:` contexts because the requested image names and tags are explicit.

## Source Layout

`src/Xeno.Core` contains models, settings, protocol contracts, provider-neutral database interfaces, provider-specific database drivers, serialization, authorization, routing models, and shared services that are safe for server and client reuse.

`src/Xeno.Server` contains the Watson 7 host, REST route registrars, WebSocket client gateway, routing execution, request history capture, authentication services, background health jobs, and server-only services.

`src/Xeno.Client` is a console application packaged as a .NET tool:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>xeno</ToolCommandName>
<Version>0.1.0</Version>
```

It must also support single-file self-contained publishes for Windows, macOS, and Linux so users can run `xeno.exe` or `./xeno` without knowing .NET tooling.

Test projects follow `BACKEND_TEST_ARCHITECTURE.md`:

- `Test.Shared` holds Touchstone descriptors and has no console output.
- `Test.Automated` runs the shared suites from the command line and returns exit code `0` only on success.
- `Test.Xunit` and `Test.Nunit` consume the same descriptors.

## C# Coding Rules

The codebase must follow the rules in `CODE_STYLE.md` and `BACKEND_ARCHITECTURE.md` from the first commit:

- no `var`;
- no tuple returns;
- `using` directives inside namespace blocks;
- classic `using (...) { }` statements instead of using declarations;
- one class or enum per file;
- XML documentation on all public classes, constructors, properties, methods, and enums;
- nullable reference types enabled in project files;
- backing fields on public properties that require null/range validation;
- `CancellationToken` accepted by async methods that do cancellable work;
- `ConfigureAwait(false)` on awaits in library and server code;
- async variants for public methods that return `IEnumerable` where appropriate;
- explicit guard clauses and specific exception types;
- provider-specific SQL in provider-specific folders;
- no `Console.WriteLine` calls in library code;
- no generic CRUD repository abstraction.

The requirements call the PostgreSQL provider folder and enum value `Postgresql`. Use that exact spelling in Xeno, even though Conductor has some `PostgreSql` paths.

## Server Configuration

The server reads JSON settings first and then applies environment overrides. `docker/xeno.json` and `docker/factory/xeno.json` must include the full shape:

```json
{
  "CreatedUtc": "2026-06-23T00:00:00.000Z",
  "Rest": {
    "Hostname": "0.0.0.0",
    "Port": 9000,
    "Ssl": false,
    "Certificate": null,
    "RequireAuthentication": true
  },
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": ["http://localhost:9100"],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["Authorization", "Content-Type", "x-token", "x-tenant-guid", "x-access-key", "x-signature"],
    "AllowCredentials": true,
    "MaxAgeSeconds": 86400
  },
  "WebSocket": {
    "Enabled": true,
    "Hostname": "0.0.0.0",
    "Port": 9001,
    "Path": "/ws/v1/clients",
    "RequireTls": false,
    "HeartbeatIntervalMs": 15000,
    "ClientTimeoutMs": 45000,
    "MaxMessageBytes": 10485760,
    "MaxPendingJobsPerClient": 4
  },
  "Database": {
    "Type": "Postgresql",
    "Filename": "data/xeno.db",
    "Server": "xeno-postgres",
    "Port": 5432,
    "DatabaseName": "xeno",
    "Username": "xeno",
    "Password": "xeno"
  },
  "Logging": {
    "ConsoleLogging": true,
    "FileLogging": true,
    "LogDirectory": "logs",
    "LogFilename": "xeno.log"
  },
  "Auth": {
    "Issuer": "xeno",
    "SigningKey": "override-via-env",
    "SessionMinutes": 60,
    "AdminApiKey": "override-via-env"
  },
  "RequestHistory": {
    "Enabled": true,
    "MaxRequestBodyBytes": 65536,
    "MaxResponseBodyBytes": 65536,
    "RetentionDays": 30,
    "BodyRetentionDays": 7,
    "MetadataRetentionDays": 30
  },
  "ModelAccessControl": {
    "Enabled": true,
    "Mode": "Monitor",
    "DefaultDecision": "Permit",
    "RequireCredentialForProxy": true,
    "UnknownModelBehavior": "Deny",
    "ListModelsBehavior": "Filter",
    "CacheTtlMs": 30000,
    "AllowAdministratorBypass": false,
    "AllowGlobalAdministratorBypass": false
  },
  "Routing": {
    "DefaultTimeoutMs": 300000,
    "QueueTimeoutMs": 30000,
    "MaxStreamingIdleMs": 60000,
    "FailureQuarantineThreshold": 5,
    "FailureQuarantineWindowMs": 300000
  }
}
```

Environment overrides use the `XENO_` prefix, for example `XENO_SETTINGS_FILE`, `XENO_DB_TYPE`, `XENO_DB_SERVER`, `XENO_DB_PASSWORD`, `XENO_AUTH_SIGNING_KEY`, `XENO_WEBSOCKET_PORT`, and `XENO_CORS_ALLOWED_ORIGINS`. Startup logs may report provider selection, ports, and migration versions, but never secrets.

## Database Architecture

Create the provider-neutral layer in `Xeno.Core/Database`:

```text
Database/
|-- DatabaseDriverBase.cs
|-- DatabaseDriverFactory.cs
|-- DatabaseSettings.cs
|-- DatabaseTypeEnum.cs
|-- SchemaMigration.cs
|-- Interfaces/
|-- Sqlite/
|   |-- SqliteDatabaseDriver.cs
|   |-- Implementations/
|   |-- Queries/
|-- Mysql/
|   |-- MysqlDatabaseDriver.cs
|   |-- Implementations/
|   |-- Queries/
|-- Postgresql/
|   |-- PostgresqlDatabaseDriver.cs
|   |-- Implementations/
|   |-- Queries/
|-- SqlServer/
    |-- SqlServerDatabaseDriver.cs
    |-- Implementations/
    |-- Queries/
```

`DatabaseDriverBase` exposes domain-specific method interfaces through protected-set properties:

- `IAccountMethods`
- `ITenantMethods`
- `IAdministratorMethods`
- `IUserMethods`
- `ICredentialMethods`
- `IAuthSessionMethods`
- `IRoleMethods`
- `IPermissionMethods`
- `IAuditMethods`
- `IRequestHistoryMethods`
- `IXenoClientMethods`
- `IXenoClientConnectionMethods`
- `IModelRunnerEndpointMethods`
- `IModelDefinitionMethods`
- `IModelConfigurationMethods`
- `IVirtualModelRunnerMethods`
- `ILoadBalancingPolicyMethods`
- `IModelAccessPolicyMethods`
- `IVirtualModelRunnerReservationMethods`
- `IInferenceJobMethods`
- `IAnalyticsSavedReportMethods`

Every provider supports tracked, idempotent migrations through a `schema_migrations` table. Every record has a string ID up to 64 characters, `createdutc`, `lastupdateutc`, and `active` unless the table is explicitly global and append-only. Tenant-owned tables include `tenantid` and all tenant-owned queries filter by `tenantid`.

SQLite writes must be serialized with `SemaphoreSlim` or a documented `ReaderWriterLockSlim` strategy.

## Identifier Prefixes

Use PrettyId K-sortable IDs with stable prefixes:

| Entity | Prefix |
| --- | --- |
| Account | `acct_` |
| Tenant | `ten_` |
| Administrator | `adm_` |
| User | `usr_` |
| Credential | `cred_` |
| Auth session | `sess_` |
| Role | `role_` |
| Permission | `perm_` |
| Audit record | `aud_` |
| Request history | `req_` |
| Xeno client | `xcl_` |
| Client connection | `xconn_` |
| Hardware snapshot | `hw_` |
| Model runner endpoint | `mre_` |
| Model definition | `md_` |
| Model configuration | `mc_` |
| Virtual model runner | `vmr_` |
| Load-balancing policy | `lbp_` |
| Model access policy | `map_` |
| Model access rule | `mar_` |
| VMR reservation | `vmrr_` |
| VMR reservation subject | `vmrrs_` |
| Inference job | `job_` |
| Analytics saved report | `asr_` |

## Core Data Model

Security and tenancy tables follow `AUTHENTICATION.md`:

- `accounts`
- `tenants`
- `admins`
- `users`
- `credentials`
- `authsessions`
- `userroles`
- `userroleassignments`
- `userrolemaps`
- `credentialscopeassignments`
- `permissions`
- `rolepermissionmaps`
- `auditrecords`

Xeno-specific control-plane tables:

- `xenoclients`: tenant, client identity, display name, machine name, OS, architecture, client version, active state, service state, labels, tags, metadata, created/updated timestamps.
- `xenoclientconnections`: client, connection ID, remote address, connected UTC, last heartbeat UTC, disconnect UTC, protocol version, authenticated principal, connection state.
- `xenohardwaresnapshots`: client, CPU summary, memory, disk, network, GPU inventory JSON, raw inventory JSON, captured UTC.
- `modelrunnerendpoints`: tenant, client ID, provider family (`Ollama`, `OpenAI`, `Gemini`, `vLLM`, `OpenAICompatible`), runner name, endpoint alias, active state, service state (`Normal`, `Draining`, `Quarantined`), health settings, max parallel requests, weight, labels, tags, metadata.
- `clientmodelinventory`: tenant, client ID, endpoint ID, model name, family, parameter size, quantization, context window, supports embeddings, supports completions, loaded state, source, captured UTC.
- `modeldefinitions`: Conductor-compatible model metadata.
- `modelconfigurations`: Conductor-compatible pinned generation and embedding settings.
- `virtualmodelrunners`: Conductor-compatible VMR configuration with endpoint IDs, model definition IDs, model configuration IDs, model-configuration mappings, load-balancing mode, load-balancing policy ID, model access policy ID, request permissions, strict mode, session affinity, request history flag, labels/tags/metadata.
- `loadbalancingpolicies`: Conductor-compatible filters, ranking rules, fallback mode, tie breaker, labels/tags/metadata.
- `modelaccesspolicies` and `modelaccessrules`: Conductor-compatible policy/rule definitions.
- `virtualmodelrunnerreservations` and `virtualmodelrunnerreservationsubjects`: Conductor-compatible reservation windows and participants.
- `inferencejobs`: request/job correlation, tenant, VMR, endpoint, client, request type, model, state, enqueue/start/complete timestamps, timeout, status code, failure reason.
- `requesthistoryentries` and `requesthistorydetails`: durable HTTP and routing diagnostics with redacted headers and truncated bodies.
- `requestanalyticsevents`, `requestanalyticssummaries`, and `analyticssavedreports`: dashboard analytics, TTFT/token/cost estimate reporting, reservation and model-access dimensions.

Indexes must support tenant-scoped enumeration, active lookups, unique names within tenant, reservation overlap detection, endpoint health/routing lookup, and request-history filters.

## Authentication, Authorization, and Accounting

Authentication normalizes every request into:

```text
(PrincipalType, PrincipalId, TenantId, AccountId?, SessionId?, AuthScheme, ResourceType, OperationType, ResourceId?)
```

Supported schemes:

- bearer session token in `Authorization: Bearer` or `x-token`;
- login headers or login DTO for user session issuance;
- direct credential headers `x-access-key`, `x-secret-key`, `x-tenant-guid`;
- signed credential requests with `x-access-key`, `x-date`, `x-nonce`, `x-signature`, `x-tenant-guid`;
- system admin API key restricted to platform administration.

Only one scheme may drive a request. If multiple credential carriers appear, they must match semantically or the server rejects the request.

Session tokens are opaque, short-lived, tenant-bound, and backed by `authsessions`. Token material uses AES-256 with a random IV per token or an equivalent authenticated encryption design. Tokens stop working when the session, principal, credential, user, or tenant is disabled.

Authorization implements the built-in roles from `AUTHENTICATION.md`: `TenantAdmin`, `SecurityAdmin`, `Auditor`, `ResourceAdmin`, `Editor`, `Viewer`, `TenantMember`, and `Custom`. Built-in roles are seeded, protected, and immutable through normal APIs. Effective permission resolution applies explicit deny before permit and invalidates caches on writes to users, credentials, sessions, roles, permissions, assignments, or principals.

Accounting persists authorization failures and privileged security events. Audit records include request ID, correlation ID, trace ID, principal, tenant, request type, resource, operation, result, denial reason, bypass reason, source IP, and status code. Request history captures normal permitted request flow; audit captures security decisions.

## Request Context

Every Watson REST request and every WebSocket job request creates or maps to a typed `RequestContext` with:

- request ID, correlation ID, trace ID;
- HTTP method, path, query, parsed URL context, request type;
- tenant, user, credential, administrator, account, session;
- authentication result and scheme;
- authorization result, resource type, operation type, matched permissions;
- VMR, selected endpoint, selected client, requested model, effective model;
- timestamps for received, routed, started upstream, first token, completed;
- source IP, user agent, redacted headers;
- raw body bytes only where request history capture allows it.

Do not implement authorization directly inside route handlers. Routes retrieve `RequestContext`, validate input DTOs, call services, set explicit response codes, and return typed DTOs.

## REST API Surface

Use Watson 7 directly:

- `WebserverSettings` and `Webserver`;
- `server.Get(...)`, `server.Post<T>(...)`, `server.Put<T>(...)`, `server.Delete(...)`;
- `ApiRequest` for typed request access;
- `Server.Routes.AuthenticateApiRequest` for request context creation;
- `Server.Routes.Preflight` for CORS `OPTIONS`;
- `Server.Routes.PostRouting` for completed-request logging, CORS response headers, and request history capture;
- `Server.UseOpenApi()` for `/openapi.json` and Swagger documentation.

Route registrar classes:

- `HealthRoutes`
- `AuthRoutes`
- `TenantRoutes`
- `UserRoutes`
- `CredentialRoutes`
- `RoleRoutes`
- `PermissionRoutes`
- `EffectivePermissionRoutes`
- `XenoClientRoutes`
- `ClientConnectionRoutes`
- `ModelRunnerEndpointRoutes`
- `ModelDefinitionRoutes`
- `ModelConfigurationRoutes`
- `VirtualModelRunnerRoutes`
- `LoadBalancingPolicyRoutes`
- `ModelAccessPolicyRoutes`
- `VirtualModelRunnerReservationRoutes`
- `RequestHistoryRoutes`
- `AnalyticsRoutes`
- `AuditRoutes`
- `BackupRestoreRoutes`
- `OpenAICompatibleRoutes`

Management routes use `/api/v1/...`. Required route groups include:

- `/api/v1/auth/token`, `/api/v1/auth/token/refresh`, `/api/v1/auth/token/details`, logout/revoke;
- `/api/v1/tenants`, `/api/v1/users`, `/api/v1/credentials`, `/api/v1/roles`, `/api/v1/permissions`;
- `/api/v1/clients`, `/api/v1/clients/{id}/connections`, `/api/v1/clients/{id}/hardware`, `/api/v1/clients/{id}/models`;
- `/api/v1/modelrunnerendpoints`, including validate, health, drain, resume, quarantine, load-model;
- `/api/v1/modeldefinitions`, `/api/v1/modelconfigurations`;
- `/api/v1/virtualmodelrunners`, including validate, health, effective, explain-routing, load-model, reservations, reservation-effective;
- `/api/v1/loadbalancingpolicies`, including validate and metrics catalog;
- `/api/v1/modelaccesspolicies`, including validate, evaluate, effective;
- `/api/v1/vmrreservations`;
- `/api/v1/requesthistory`, `/api/v1/requesthistory/summary`, `/api/v1/requesthistory/analytics/overview`, `/api/v1/requesthistory/{id}`, `/api/v1/requesthistory/bulk`;
- `/api/v1/analytics/ttft`, `/api/v1/analytics/tokens`, `/api/v1/analytics/costs`, `/api/v1/analytics/savedreports`;
- `/api/v1/audit`;
- `/api/v1/backup`, `/api/v1/restore`;
- `/api/v1/openai/v1/chat/completions`, `/api/v1/openai/v1/completions`, `/api/v1/openai/v1/embeddings`, `/api/v1/openai/v1/models`.

Protocol-compatible aliases:

- `POST /v1/chat/completions`
- `POST /v1/completions`
- `POST /v1/embeddings`
- `GET /v1/models`

The alias routes must use the same request context, authentication, routing, request history, and OpenAPI documentation notes as the management path.

## WebSocket Control Plane

Use a dedicated server component backed by Watson-compatible WebSocket tooling. The REST server remains Watson 7; the WebSocket listener may use `WatsonWebsocket` if Watson 7 does not host the upgrade path directly.

The WebSocket endpoint is `/ws/v1/clients`. Clients authenticate during handshake with a tenant-bound credential or bootstrap token. The server rejects connections where the client ID, credential tenant, and requested tenant conflict.

Message envelope:

```json
{
  "ProtocolVersion": "1.0",
  "Type": "client.register",
  "MessageId": "msg_...",
  "CorrelationId": "req_...",
  "TenantId": "ten_...",
  "ClientId": "xcl_...",
  "TimestampUtc": "2026-06-23T12:00:00.000Z",
  "Payload": {}
}
```

Fixed message types must have typed payload classes. Raw request/response payload bytes are allowed only for provider protocol bodies and must remain isolated from control-plane contracts.

Client-to-server messages:

- `client.register`: client version, OS, architecture, machine name, configured providers, labels/tags, limits.
- `client.inventory`: CPU, memory, disk, GPU, available models, loaded models, provider health.
- `client.heartbeat`: connection health and in-flight job counts.
- `job.accepted`: client accepted a routed job.
- `job.rejected`: client rejected before execution with reason code.
- `job.chunk`: streaming response chunk.
- `job.completed`: final non-stream response or stream terminal metadata.
- `job.failed`: provider or client failure with typed error.
- `model.load.completed`: model-load result.
- `metrics.snapshot`: point-in-time runtime metrics for policy evaluation.

Server-to-client messages:

- `server.registered`: assigned/confirmed client ID, server time, heartbeat interval.
- `server.ping`: heartbeat challenge.
- `job.invoke`: OpenAI-compatible request body, route metadata, timeout, streaming flag, effective model, pinned configuration.
- `job.cancel`: cancellation request tied to a job ID.
- `model.load`: provider-specific load or verify request.
- `client.config.refresh`: ask client to reload local settings and re-advertise.
- `server.shutdown`: planned server shutdown hint.

Server connection state is authoritative. A disconnected client is unavailable for new work. In-flight streaming jobs fail closed if the client disconnects unless the response has already ended.

## Xeno Client

The client stores settings at `~/.xeno/client.json`. It creates the directory with owner-only permissions where the OS supports them. Secrets should use environment variable references or OS-protected secret storage when available. Plaintext secrets in config are allowed only with an explicit warning from the CLI.

Client settings shape:

```json
{
  "ClientId": null,
  "TenantId": "ten_default",
  "Name": "workstation-01",
  "Server": {
    "Url": "wss://xeno.example.com/ws/v1/clients",
    "AccessKey": null,
    "SecretKey": null,
    "Token": null,
    "ReconnectInitialMs": 1000,
    "ReconnectMaxMs": 30000,
    "HeartbeatIntervalMs": 15000
  },
  "Hardware": {
    "AdvertiseCpu": true,
    "AdvertiseMemory": true,
    "AdvertiseGpu": true,
    "RefreshIntervalMs": 30000
  },
  "Runners": [
    {
      "Name": "local-ollama",
      "Provider": "Ollama",
      "Endpoint": "http://127.0.0.1:11434",
      "ApiKey": null,
      "MaxParallelRequests": 2,
      "Weight": 100,
      "Models": [
        {
          "Name": "llama3.2:latest",
          "SupportsCompletions": true,
          "SupportsEmbeddings": false,
          "ContextWindowSize": 8192,
          "Labels": ["local", "chat"]
        }
      ],
      "Labels": ["gpu"],
      "Tags": {
        "site": "home-lab"
      }
    }
  ]
}
```

CLI commands:

- `xeno help`
- `xeno version`
- `xeno init`
- `xeno config show`
- `xeno config set <path> <value>`
- `xeno server set-url <url>`
- `xeno auth set-token <token>`
- `xeno auth set-credential <access-key> <secret-key>`
- `xeno runner add --name <name> --provider <ollama|openai|gemini|openai-compatible|vllm> --endpoint <url>`
- `xeno runner list`
- `xeno runner remove <name>`
- `xeno runner test <name>`
- `xeno model add --runner <name> --name <model> --completions true --embeddings false`
- `xeno model list`
- `xeno model remove --runner <name> --name <model>`
- `xeno inventory`
- `xeno connect`
- `xeno status`
- `xeno service install`
- `xeno service uninstall`

`xeno connect` maintains a persistent WebSocket with exponential backoff and jitter. It re-registers after reconnect, re-sends inventory, and does not accept jobs until registration is acknowledged.

Provider adapters:

- Ollama: native `/api/generate`, `/api/chat`, `/api/embed`, `/api/embeddings`, `/api/tags`, `/api/ps`, `/api/show`.
- OpenAI: `/v1/models`, `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`.
- vLLM/OpenAI-compatible: same OpenAI-compatible adapter with configurable base path and headers.
- Gemini: Gemini generate, stream generate, embed, and list models mapped from OpenAI-compatible request DTOs where possible.

The client must support streaming. For OpenAI-compatible streaming requests, the server relays SSE chunks from `job.chunk` messages in order. The client honors `job.cancel`, provider timeout, and server disconnect cancellation.

## Routing Parity With Conductor

Xeno routing must match Conductor behavior, with WebSocket clients replacing direct upstream endpoints.

Routing order:

1. Resolve the target VMR from route, host, explicit selector, or default OpenAI-compatible route policy.
2. Resolve tenant and request identity.
3. Evaluate active VMR reservations and drain windows.
4. Apply request-type gates: embeddings, completions, and model management.
5. Resolve requested/effective model and strict-mode rules.
6. Evaluate model access policy.
7. Build endpoint candidates from connected Xeno clients and their advertised runner endpoints.
8. Check session affinity before ordinary candidate selection.
9. Exclude endpoints that are inactive, quarantined, unhealthy, disconnected, saturated, or missing required model capability. Draining endpoints are excluded for new work but may serve existing pins.
10. If an active load-balancing policy is attached, apply filters, ranking, telemetry freshness, tie-breaker, and fallback mode.
11. If no policy selects a candidate, apply the VMR's legacy `LoadBalancingMode`.
12. Create or refresh session affinity pins when configured.
13. Dispatch the job over WebSocket and stream or collect the result.
14. Capture request history, analytics, audit events, and routing decision details.

Legacy load-balancing modes:

- `RoundRobin`
- `Random`
- `FirstAvailable`

Endpoint service states:

- `Normal`
- `Draining`
- `Quarantined`

Session affinity modes:

- `None`
- `SourceIP`
- `ApiKey`
- `Header`

Session affinity settings mirror Conductor: header name for `Header`, timeout clamped from one minute to twenty-four hours, and maximum entries clamped from 100 to 1,000,000. Pins are in-memory for v0.1.0 unless HA requirements are added later; configuration changes remove affected pins.

## Load-Balancing Policies

Policies are tenant-scoped resources attached to VMRs with `LoadBalancingPolicyId`.

Policy fields:

- `Name`
- `Description`
- `MaxTelemetryAgeMs`
- `Filters`
- `Ranking`
- `FallbackMode`: `UseLegacyLoadBalancingMode` or `FailClosed`
- `TieBreaker`: `RoundRobin`, `Random`, or `FirstAvailable`
- `Active`
- `Labels`
- `Tags`
- `Metadata`

Filter operators:

- `Equal`
- `NotEqual`
- `LessThan`
- `LessThanOrEqual`
- `GreaterThan`
- `GreaterThanOrEqual`

Value types:

- `Number`
- `Boolean`
- `String`

Built-in metrics must include the Conductor catalog plus Xeno-specific connection metrics:

| Metric ID | Type | Filter | Rank | Direction |
| --- | --- | --- | --- | --- |
| `health.isHealthy` | Boolean | Yes | No | n/a |
| `health.hasCapacity` | Boolean | Yes | No | n/a |
| `health.inFlightRequests` | Number | Yes | Yes | Ascending |
| `endpoint.weight` | Number | Yes | Yes | Descending |
| `endpoint.maxParallelRequests` | Number | Yes | Yes | Descending |
| `client.connected` | Boolean | Yes | No | n/a |
| `client.latencyMs` | Number | Yes | Yes | Ascending |
| `client.queueDepth` | Number | Yes | Yes | Ascending |
| `client.cpu.utilizationPercent` | Number | Yes | Yes | Ascending |
| `client.memory.utilizationPercent` | Number | Yes | Yes | Ascending |
| `client.memory.availableBytes` | Number | Yes | Yes | Descending |
| `client.gpu.available` | Boolean | Yes | No | n/a |
| `client.gpu.avgUtilizationPercent` | Number | Yes | Yes | Ascending |
| `client.gpu.minFreeMemoryMegabytes` | Number | Yes | Yes | Descending |
| `client.gpu.maxTemperatureCelsius` | Number | Yes | Yes | Ascending |
| `runner.ollama.available` | Boolean | Yes | No | n/a |
| `runner.ollama.loadedModelCount` | Number | Yes | Yes | Descending |

The metric catalog route returns the live catalog. Validation rejects unknown metrics, incompatible value types, nonnumeric ranking metrics, and ranking rules with `Weight <= 0`.

## Model Access Policies

Model access policies mirror Conductor:

- policy default decision: `Permit` or `Deny`;
- enforcement mode from settings: `Disabled`, `Monitor`, `Enforce`;
- unknown model behavior: `Deny`, `Permit`, `RequireStrictVirtualModelRunnerResolution`;
- list-models behavior: `Filter`, `Synthesize`, `RawPassThrough`;
- rules ordered by descending `Priority`;
- same-priority `Deny` wins over `Allow`;
- no matching rule falls back to policy default, then server default.

Subjects:

- `Credential`
- `CredentialLabel`
- `User`
- `UserLabel`
- `Tenant`
- `Any`

Resources:

- `ModelDefinition`
- `ModelName`
- `ModelLabel`
- `VirtualModelRunner`
- `Any`

Actions:

- `Completions`
- `Embeddings`
- `ListModels`
- `ShowModel`
- `LoadModel`
- `UnloadModel`
- `ModelManagement`

Selectors support `label`, `labels`, `value`, `equals`, `prefix`, and `contains`. Monitor-mode would-deny decisions are persisted in request history and analytics without blocking the request.

## Reservations

VMR reservations are tenant-scoped admission gates. They do not replace RBAC, model access policy, endpoint health, load balancing, or provider checks.

Reservation fields:

- `TenantId`
- `VirtualModelRunnerId`
- `Name`
- `Description`
- `StartUtc`
- `EndUtc`
- `AdmissionDrainLeadMs`
- `Active`
- `Subjects`

Subject types:

- `User`
- `Credential`

Validation rules:

- tenant ID is required for management operations;
- VMR must be active and in the same tenant;
- start must be before end;
- at least one subject is required;
- user and credential subjects must exist, be active, and belong to the tenant;
- duplicate subjects are invalid;
- active reservations cannot overlap for the same tenant and VMR;
- drain lead must be nonnegative and within the configured maximum.

Runtime behavior:

- no active reservation: continue routing;
- one active reservation and matching participant: continue normal routing;
- one active reservation and no usable identity: `401 ReservationAuthenticationRequired`;
- one active reservation and nonparticipant: `403 ReservationDenied`;
- drain-window denial: `403 ReservationDrainDenied`;
- overlapping active reservations: `503 ReservationConflict`.

Request history and analytics include `ReservationGuid`, `ReservationName`, `ReservationDecision`, `ReservationReasonCode`, `ReservationWindowStartUtc`, and `ReservationWindowEndUtc`.

## Model Loading

Model loading is a management-plane operation, not a normal proxied inference request. It is not written as an inference request history entry, but it is logged, audited, and counted in metrics.

Routes:

- `POST /api/v1/modelrunnerendpoints/{id}/load-model`
- `POST /api/v1/virtualmodelrunners/{id}/load-model`

Request fields mirror Conductor:

- `Model`
- `ModelDefinitionId`
- `ProbeKind`: `Auto`, `MetadataOnly`, `ChatCompletion`, `Completion`, `Embeddings`, `NativeGenerate`
- `TargetMode`: `SelectedEndpoint`, `AllEligibleEndpoints`, `AllConfiguredEndpoints`, `SpecificEndpointIds`
- `EndpointIds`
- `InputText`
- `KeepAlive`
- `TimeoutMs`
- `MaxRetries`
- `VerifyLoaded`
- `IncludeInactive`
- `DryRun`

Provider behavior:

- Ollama uses generate/embed and `/api/ps` verification.
- vLLM and OpenAI-compatible providers verify via `/v1/models` unless an explicit probe is requested.
- OpenAI verifies remotely and treats probes as potentially billable.
- Gemini verifies remotely and treats probes as potentially billable.

Stable outcomes include `Loaded`, `AlreadyAvailable`, `Verified`, `VerifiedRemote`, `DryRun`, `Skipped`, `Failed`, `TimedOut`, `UnauthorizedUpstream`, `ModelRequired`, `ModelNotAttached`, `NoEligibleEndpoints`, `ClientDisconnected`, and `UnsupportedApiType`.

## Request History and Analytics

Request capture is enabled by default and runs from Watson `PostRouting`, with WebSocket job metadata folded into the same entry after routing completes.

Capture rules:

- redact `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, any `*api-key*`, any `*token*`, access keys, secret keys, signatures, and provider keys;
- truncate request and response bodies by settings and store original byte lengths and `Truncated` flags;
- store tenant, user, credential, principal name, VMR, endpoint, client, requested model, effective model, routing decision, model access fields, reservation fields, status, duration, TTFT, token usage where available;
- run a cleanup service for metadata retention and body retention.

The dashboard depends on:

- summary buckets for hour/day/week/month;
- searchable request rows with pagination;
- detail records with bodies and routing decisions;
- bulk delete by filter;
- analytics overview for traffic, failures, denials, TTFT, token usage, estimate-only costs, provider breakdowns, model breakdowns, endpoint/client breakdowns, reservation filters, and model-access filters.

## Dashboard

Use the required frontend stack:

- React 19.x;
- Vite 6.x;
- React Router 7.x;
- browser Fetch API behind a hand-written `ApiClient`;
- `i18next`, `react-i18next`, and `i18next-browser-languagedetector`;
- no axios;
- no charting library.

Required structure:

```text
dashboard/
|-- public/
|-- src/
|   |-- main.jsx
|   |-- App.jsx
|   |-- index.css
|   |-- views/
|   |-- components/
|   |-- context/
|   |-- hooks/
|   |-- utils/
|   |-- i18n/
|-- package.json
|-- package-lock.json
|-- vite.config.js
|-- Dockerfile
|-- nginx.conf
|-- docker-entrypoint.sh
```

Required first-release views:

- Login with language selector.
- Home with summary tiles, manual refresh, range selector, and hand-rolled stacked SVG activity chart.
- Request History with filters, chart, table, pagination, drill-down modal, and bulk delete.
- API Explorer driven by `/openapi.json`.
- Tenants, users, credentials, roles, permissions, and effective permissions.
- Xeno Clients with live connection state, hardware inventory, runner endpoints, models, labels/tags, drain/resume/quarantine controls.
- Virtual Model Runners with endpoint attachment, strict mode, model definitions, model configurations, session affinity, load-balancing policy, model access policy, effective configuration, explain-routing, and load-model.
- Load-Balancing Policies with metric catalog, create/edit/validate, JSON inspection, examples, and attach workflow.
- Model Access Policies with rule builder, validate/evaluate/effective workflows, monitor/enforce diagnostics.
- Reservations with create/edit/validate/deactivate/effective-access workflows.
- Analytics with TTFT, token, cost estimate, failure, denial, model, client, endpoint, reservation, and model-access filters.
- Settings with server settings visibility, i18n language selector, and safe operational toggles.

Dashboard controls must be permission-aware through effective-permission API results, but the server still enforces every operation.

## Internationalization

I18n is part of the first dashboard slice, not a cleanup task.

Required files:

- `src/i18n/index.js`
- `src/i18n/localeRegistry.js`
- `src/i18n/resources.js`
- `src/i18n/formatters.js`
- `src/components/LanguageSelector.jsx`

The locale registry includes default locale, supported locales, aliases, storage key, direction metadata, and pseudo-locales for expansion and RTL checks. The runtime updates `document.documentElement.lang` and `document.documentElement.dir`, persists locale across refresh/logout/login/deep links, and loads before meaningful UI paint.

All visible and accessibility-facing strings go through the translation runtime. Raw display enums are prohibited; use a display-label layer. Format dates, times, relative times, durations, numbers, percentages, bytes, and lists through explicit-locale helpers.

Minimum QA locales:

- English baseline;
- German or another longer Latin locale;
- Japanese or another CJK locale;
- RTL or RTL pseudo-locale;
- expansion pseudo-locale.

Automated checks must catch missing keys, orphaned keys, hard-coded user-facing strings outside resource files, formatter behavior, `lang`/`dir` synchronization, and locale persistence.

## Docker and Factory Reset

Server image:

- build from `src/Xeno.Server/Dockerfile`;
- final tag `jchristn77/xeno-server:v0.1.0`;
- expose REST port 9000 and WebSocket port 9001;
- mount `/app/data`, `/app/logs`, and `/app/xeno.json`;
- run health check against the Watson health route.

Dashboard image:

- build from `dashboard/Dockerfile`;
- final tag `jchristn77/xeno-dashboard:v0.1.0`;
- serve through nginx on port 9100;
- generate runtime config from environment variables such as `XENO_SERVER_URL`.

`docker/compose.yaml` includes:

- `xeno-server`;
- `xeno-dashboard`;
- `xeno-postgres` plus init service;
- optional profiles for `sqlite`, `mysql`, and `sqlserver`;
- named volumes for database data;
- mounted local data/log/settings paths for easy inspection.

`docker/factory/reset.bat` and `reset.sh` stop compose services, remove Xeno containers/volumes created by the compose file, recreate data/log directories, copy factory settings, and start the deployment. The scripts must avoid deleting arbitrary paths and should only operate inside `docker/`.

## Documentation

Root documentation required for v0.1.0:

- `README.md`: product overview, architecture, quick start, local client setup, Docker deployment, first OpenAI-compatible request, dashboard login, troubleshooting.
- `CHANGELOG.md`: `0.1.0` initial release section.
- `REST_API.md`: generated or manually maintained route reference with auth levels, request/response DTOs, examples, error codes, pagination, OpenAI-compatible routes, WebSocket protocol notes.
- `AGENTS.md`: coding rules, repo structure, build/test commands, requirements pointers.
- `Xeno.postman_collection.json`: management routes, token flow, OpenAI routes, routing explanation, reservation evaluation, model loading.

The OpenAPI spec is the source of truth for the dashboard API Explorer. `REST_API.md` and Postman must be checked against it before release.

## Implementation Phases

### Phase 1: Repository and project scaffolding

Create root files, solution, projects, Docker skeleton, dashboard skeleton, build scripts, and initial settings files. Add package references for Watson 7, PrettyId, database providers, serialization, logging, Touchstone test packages, and WebSocket support.

Acceptance:

- solution restores;
- all `.csproj` files show version `0.1.0`;
- Docker scripts build images with requested names and tags;
- root structure matches this plan;
- no source code is outside `src/`, `dashboard/`, `test/`, or `sdk/` except scripts/docs/config.

### Phase 2: Core models, settings, serialization, and IDs

Implement settings classes, ID generator, strict enum serialization, DTO response envelope, error response shape, and all core entity models.

Acceptance:

- model tests cover validation and default values;
- settings load from JSON and environment;
- numeric settings clamp to documented ranges;
- all public members are documented;
- no fixed-contract code relies on `JsonElement` navigation.

### Phase 3: Database providers and migrations

Implement `DatabaseDriverBase`, `DatabaseDriverFactory`, migrations, provider folders, query classes, method interfaces, and first-boot seeding for SQLite, MySQL, PostgreSQL, and SQL Server.

Acceptance:

- every provider initializes an empty database;
- migration table records applied versions;
- seed data is idempotent;
- tenant-scoped CRUD tests pass across all providers;
- SQLite write concurrency is explicitly serialized.

### Phase 4: Authentication, RBAC, sessions, audit

Build tenant resolution, auth session issuance/validation/revocation, credential generation, signed request validation, built-in role seeding, effective permission resolution, authorization cache invalidation, and audit persistence.

Acceptance:

- conflicting tenant hints are rejected;
- bearer and `x-token` must match when both are present;
- credential secrets are shown once and redacted afterward;
- authorization denials persist audit records;
- admin endpoint compatibility fallback is never used;
- effective permission endpoints work for users and credentials.

### Phase 5: Watson REST host and OpenAPI

Build the thin `Program.cs`, instance-based `XenoServer`, route registrars, CORS preflight, post-routing capture, health routes, OpenAPI, and typed REST DTOs.

Acceptance:

- management routes are under `/api/v1`;
- OpenAI-compatible aliases work under `/v1`;
- `/openapi.json` loads and includes route descriptions;
- CORS preflight and post-routing headers work;
- request context is attached to every authenticated request.

### Phase 6: WebSocket gateway and client protocol

Implement server-side client connection registry, handshake authentication, message envelope, typed payload dispatch, heartbeat, timeout, reconnect handling, job dispatch, streaming chunks, cancellation, and disconnect cleanup.

Acceptance:

- a client can register, heartbeat, advertise inventory, receive a job, stream chunks, complete a job, and reconnect;
- disconnected clients are removed from routing candidates;
- stale heartbeats mark clients unavailable;
- server rejects unauthenticated or tenant-conflicting clients.

### Phase 7: Xeno client CLI and provider adapters

Implement `xeno` CLI, settings editing, runner/model configuration commands, inventory collection, persistent connection loop, provider adapters, streaming support, and service install/uninstall.

Acceptance:

- `dotnet tool install` exposes `xeno`;
- self-contained Windows/macOS/Linux publishes run;
- `~/.xeno/client.json` is created and modified by CLI commands;
- Ollama, OpenAI, Gemini, and OpenAI-compatible/vLLM adapters can test connectivity and execute representative completions/embeddings;
- reconnect backoff works without losing local configuration.

### Phase 8: Conductor-compatible routing controls

Implement VMRs, endpoint inventory, model definitions, model configurations, model mutation, strict mode, session affinity, load-balancing policy evaluator, model access policy evaluator, reservations, effective config, explain-routing, and model loading.

Acceptance:

- routing order matches the parity section;
- policy validation rejects invalid metrics/operators/types;
- reservation denials return the required status/reason codes;
- explain-routing returns timeline, candidates, selected endpoint/client, policy evidence, model mutation summary, model access decision, and reservation decision;
- load-model works for direct endpoint and VMR target modes.

### Phase 9: Request history, analytics, metrics, backup/restore

Implement request capture, body truncation, redaction, retention cleanup, search, summary, detail, bulk delete, analytics queries, Prometheus-style metrics export, and backup/restore of configuration entities.

Acceptance:

- Home and Request History dashboard endpoints return required bucket counts;
- detail records include routing decision and redacted headers;
- analytics supports VMR, endpoint, client, provider, model, model-access, reservation, status, TTFT, tokens, and estimated cost filters;
- backup/restore validates tenant references and reservation overlaps.

### Phase 10: Dashboard

Build the React/Vite dashboard with i18n foundation, auth context, API client, protected routes, required operator pages, Conductor-compatible routing controls, Xeno client operations, and API Explorer.

Acceptance:

- login and authenticated topbar both expose language selection;
- Home, Request History, Request Details modal, and API Explorer meet the frontend requirements;
- no chart library or axios is added;
- all display strings are localizable;
- desktop, tablet, and mobile screenshots show no overlapping text or broken controls.

### Phase 11: Test matrix and release hardening

Finish shared Touchstone suites, xUnit/NUnit adapters, provider matrix integration tests, WebSocket tests, client/provider tests, dashboard tests, Docker smoke tests, and documentation refresh.

Acceptance:

- `dotnet build src/Xeno.sln` succeeds without warnings that indicate code quality issues;
- `dotnet run --project src/Test.Automated -- --results results.json` succeeds;
- xUnit and NUnit suites pass;
- provider matrix covers SQLite, MySQL, PostgreSQL, and SQL Server;
- dashboard `npm test` and `npm run build` pass;
- Docker compose starts server and dashboard;
- Postman collection and `REST_API.md` match OpenAPI.

## Required Test Coverage

Shared suites:

- ID generation and model validation.
- Settings load, validation, and environment overrides.
- Database migrations for four providers.
- First-boot seed idempotency.
- Tenant CRUD, user CRUD, credential CRUD.
- Tenant-scoped enumeration and cross-tenant denial.
- Auth session issuance, refresh, validation, revocation.
- Credential direct and signed request auth, replay protection, constant-time comparison behavior.
- RBAC resolution, explicit deny, implicit deny, admin bypass, credential owner ceiling.
- Audit denials and security event persistence.
- Client registration and heartbeat.
- Hardware/model inventory persistence.
- Routing order, VMR gates, strict mode, model mutation.
- Session affinity creation, reuse, expiry, and drain behavior.
- Load-balancing policy filtering, ranking, telemetry freshness, fallback, fail-closed.
- Model access policy monitor and enforce modes.
- Reservation validation, overlap detection, drain window, conflict handling.
- WebSocket job success, streaming success, cancellation, timeout, client disconnect.
- Request history redaction/truncation/search/summary/detail/bulk delete.
- Analytics filters and aggregation.
- Model loading target selection and provider outcomes.

Provider matrix suites run against `Sqlite`, `Mysql`, `Postgresql`, and `SqlServer`. Tests must generate domain IDs with PrettyId helpers, not raw GUIDs, unless the value is a temporary file or external test fixture.

## Requirements Conformance Checks

### Check 1: Direct trace to requirement files

| Requirement file | Xeno plan commitment |
| --- | --- |
| `REPOSITORY_REQUIREMENTS.md` | Required root files, source confined to approved folders, `.dockerignore`, MIT `LICENSE.md`, Docker `.yaml`, explicit image names/tags. |
| `BACKEND_ARCHITECTURE.md` | Watson 7 only for REST, route registrars, Preflight/PostRouting, typed DTOs, request context, PrettyId, provider-neutral database with four providers, request history, Docker ops, strict C# style. |
| `AUTHENTICATION.md` | Tenant resolution, users, credentials, sessions, RBAC, built-in roles, effective permissions, audit, admin bypass rules, tenant isolation, response codes, secret handling. |
| `BACKEND_TEST_ARCHITECTURE.md` | `Test.Shared`, `Test.Automated`, `Test.Xunit`, `Test.Nunit`, Touchstone descriptors, provider matrix, CI-ready commands. |
| `FRONTEND_ARCHITECTURE.md` | React/Vite/Router/fetch stack, required Home/Request History/API Explorer pages, hand-rolled SVG charts, Docker/nginx packaging, dashboard structure. |
| `I18N.md` | i18next runtime, locale registry, language selectors pre/post auth, persisted locale, pseudo-locales, explicit formatters, hard-coded string checks. |
| `CODE_STYLE.md` | No `var`, no tuples, namespace/using placement, XML docs, cancellation tokens, `ConfigureAwait(false)`, one class/enum per file, nullable enabled. |
| `WRITING_DOCUMENTS.md` | Planning document uses direct product-specific language, avoids generic filler, and includes concrete acceptance gates. |
| `EXAMPLE_APPLICATIONS.md` | Verbex for database providers, Conductor for routing/tests/dashboard controls, Hydra for i18n, Wilson/Constellation for small project structure. |

### Check 2: Conductor parity

Xeno keeps all Conductor controls that affect routing and operations:

- Virtual Model Runners;
- Model Runner Endpoints;
- Model Definitions;
- Model Configurations;
- Model Configuration Mappings;
- Load-Balancing Modes;
- Load-Balancing Policies;
- Policy metric catalog, filters, ranking, fallback, tie-breakers;
- Model Access Policies;
- Reservations and reservation-effective evaluation;
- Session Affinity;
- Endpoint service states: normal, draining, quarantined;
- Health and capacity filtering;
- Strict model mode;
- Request type gates;
- Model mutation/pinned properties;
- Effective VMR configuration;
- Explain Routing;
- Model loading;
- Request History and Analytics dimensions for routing, model access, and reservations.

The execution transport changes from direct HTTP provider calls to server-to-client WebSocket jobs, but the decision model and operator controls remain the same.

### Check 3: Explicit non-deviations

- No SwiftStack abstraction over Watson 7.
- No generic CRUD repository as the database boundary.
- No SQLite-only shortcut.
- No `PostgreSql` enum/folder spelling in Xeno; use required `Postgresql`.
- No dashboard axios dependency.
- No charting library.
- No English-only dashboard foundation.
- No unmanaged tenant IDs trusted from ordinary callers.
- No admin endpoint access through unassigned-user compatibility fallback.
- No plaintext secret logging or request-history capture.
- No raw provider/client strings shown directly as UI labels without a localization/display layer.
- No Docker `.yml` files.

## Release Gate for v0.1.0

Xeno v0.1.0 is releasable only when the following are true:

- server, dashboard, and client build from a clean checkout;
- server and client `.csproj` files show `0.1.0`;
- Docker images build as `jchristn77/xeno-server:v0.1.0` and `jchristn77/xeno-dashboard:v0.1.0`;
- Docker compose starts a usable server/dashboard stack;
- a `xeno` client connects, advertises inventory, and completes at least one chat completion and one embedding request through the server;
- routing explanation proves the selected client/endpoint and records policy evidence;
- request history shows the routed request with redacted secrets;
- PostgreSQL, MySQL, SQL Server, and SQLite provider tests pass;
- dashboard i18n, Home, Request History, API Explorer, Clients, VMRs, policies, and reservations are usable at desktop, tablet, and mobile widths;
- `README.md`, `REST_API.md`, `CHANGELOG.md`, `AGENTS.md`, OpenAPI, and Postman collection are current.

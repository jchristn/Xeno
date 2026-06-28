# Security Improvements Plan

Xeno needs a hard authorization boundary before the alpha security model can be treated as a real multitenant control plane. The current code has useful pieces: tenant IDs on records, explicit user admin flags, credential and bearer authentication, seeded roles, audit records, and a shared Touchstone test harness. The missing piece is stricter privilege evaluation. Admin flags can be written through ordinary user update flows, tenant visibility is not tied cleanly to the caller's authority, and OpenAI-compatible routes are reachable without a runtime permission check when REST authentication is disabled.

Security work should land as a product change, not a narrow backend patch. Backend authorization, dashboard affordances, SDK behavior, OpenAPI/Postman contracts, README guidance, and test coverage all need to agree on the same rules.

## Progress Legend

Use this legend while annotating the plan:

- `Not Started`
- `In Progress`
- `Deferred`
- `Implemented`
- `Tested`
- `Documented`
- `Complete`

## Required Security Contract

The implementation must make the following statements true in code, tests, docs, and API examples.

1. A system admin can perform any action on any tenant, including creating tenants, managing users, changing `isAdmin`, changing `isTenantAdmin`, managing roles, managing permissions, managing credentials, inspecting request history, and using OpenAI-compatible routes when the tenant context is valid.
2. A tenant admin can perform any action inside only their own tenant, except changing `isAdmin`. Tenant admins can change `isTenantAdmin` only for users in their own tenant.
3. A regular user cannot change `isAdmin` or `isTenantAdmin`, directly or indirectly.
4. A tenant admin cannot change `isAdmin`, even inside their own tenant.
5. RBAC users and credentials can call only the APIs allowed by their effective tenant-scoped permissions. Role and permission grants must not let a grantor assign capabilities they do not already hold.
6. A caller authenticated for tenant A cannot operate on tenant B unless the caller is a system admin using an explicitly supported cross-tenant administrative path.
7. OpenAI-compatible APIs require valid authentication and authorization. Every model list, completion, chat completion, and embedding request must be attributable to a principal and tenant for usage reporting.
8. Authentication mechanisms must be explicit: bearer session, `x-token` session, or direct access key plus secret. Anonymous compatibility must not apply to production inference routes.
9. Authorization failures must be auditable with enough context to explain the denial without leaking foreign-tenant data.

## Security Model Decisions

Several decisions should be made once and then reflected consistently everywhere.

| Decision | Target | Status | Owner | Notes |
| --- | --- | --- | --- | --- |
| System admin scope | `User.IsAdmin == true` grants platform-wide authority across tenants. | Implemented | Codex | Central authorization service treats active user system admins as platform-wide authorities. |
| Tenant admin scope | `User.IsTenantAdmin == true` grants full authority only when target tenant equals the user's tenant. | Implemented | Codex | Tenant-admin bypass is constrained by actor and target tenant. |
| Admin flag mutation | `isAdmin` can be changed only by system admins. `isTenantAdmin` can be changed by system admins or same-tenant tenant admins. | Implemented | Codex | User create/update handlers validate security flags separately from profile fields. |
| Tenant resolution | Route, header, token, credential, and body tenant hints must agree unless a system-admin route explicitly permits cross-tenant selection. | Implemented | Codex | Token and credential auth require active matching tenants; tenant visibility checks use authority. |
| RBAC ceiling | Grantors cannot grant permissions, roles, role memberships, or credential scopes above their own effective authority. | Implemented | Codex | Roles, permissions, and role assignments validate grantor authority before save. |
| OpenAI authorization | OpenAI-compatible routes require an authenticated principal with an inference permission in the selected tenant. | Implemented | Codex | Runtime routes now require `Inference.Read` or `Inference.Execute`. |
| Usage attribution | Request history and future usage metering must persist tenant ID, principal type, principal ID, user ID, credential ID, session ID, and auth scheme when available. | Implemented | Codex | Request history now carries principal/session/auth scheme fields. |
| Compatibility mode | Development-only anonymous fallback must not authorize production management or inference surfaces. | Implemented | Codex | REST no longer maps anonymous callers to seeded admins. |

## Backend Implementation Plan

Backend work should start by consolidating privilege decisions into one small, testable authorization layer. Scattering special cases through route handlers will make the product brittle and will guarantee future bypasses.

### 1. Add A Central Authorization Kernel

Create an internal authorization service responsible for converting a request and target resource into a single decision object. The service should not write HTTP responses. It should answer: allowed or denied, status code, reason code, target tenant, matched permission source, and audit context.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| Define a decision model such as `AuthorizationDecision` with `Allowed`, `StatusCode`, `ReasonCode`, `TenantId`, `ResourceType`, `OperationType`, `PrincipalType`, `PrincipalId`, and optional matched permission metadata. | Implemented | Codex | Added `AuthorizationDecision`. |
| Define an authorization input model that includes request context, target tenant, resource type, operation type, optional resource ID, and a flag for system-admin cross-tenant operations. | Implemented | Codex | Added `AuthorizationRequest`. |
| Move system admin, tenant admin, RBAC, explicit deny, implicit deny, and tenant-fence logic into this service. | Tested | Codex | Added `AuthorizationService`; shared xUnit/NUnit/Automated suites pass. |
| Require every management and OpenAI route to call the service before touching tenant data. | Implemented | Codex | Management checks now use authorization service; OpenAI and request-history routes are gated. |
| Return 401 only for authentication failures and 403 for authenticated authorization failures. | Implemented | Codex | `AuthorizationDecision` carries status codes; authenticated authorization denials return 403. |
| Persist audit records for every denial and for privileged admin bypasses. | Implemented | Codex | Central guard audits denials and system/tenant-admin bypasses; direct grant/admin-flag denials append audit records. |

### 2. Correct Principal And Tenant Semantics

Tenant visibility must come from authority, not from `ten_default`. A default tenant is just a tenant. It must never behave like a platform-wide namespace.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| Replace `IsTenantVisible(context, tenantId)` with an authority-aware method that checks authenticated principal authority. | Implemented | Codex | Default tenant no longer acts as a global visibility bypass. |
| System admins may specify any active target tenant for tenant-scoped operations. | Implemented | Codex | `CanAccessTenant` allows active system-admin users across tenants. |
| Tenant admins may target only their own tenant. | Implemented | Codex | Tenant admins are denied with `TenantMismatch` outside their tenant. |
| Regular users and credentials may target only the tenant in their session or credential record. | Implemented | Codex | Tenant mismatch checks apply before RBAC permission checks. |
| Reject conflicting tenant hints across route, query, body, `x-tenant-guid`, `x-tenant-id`, session, and credential material. | Tested | Codex | Current REST surfaces validate session/credential/header/body tenant alignment; future route/query tenant selectors must call the same guard. |
| Add a denial reason for tenant mismatch, separate from missing permission. | Implemented | Codex | Central authorization service and route helpers use `TenantMismatch`. |

### 3. Protect User Admin Flags

Admin flags are security boundaries, not profile fields. Treat them as privileged security mutations.

| Scenario | Expected Result | Status | Owner | Notes |
| --- | --- | --- | --- | --- |
| System admin creates user with `isAdmin = true`. | Allowed. | Implemented | Codex | Covered by user create security-flag guard. |
| System admin creates user with `isTenantAdmin = true` in any tenant. | Allowed. | Implemented | Codex | System-admin bypass authorizes tenant-admin flag changes. |
| Tenant admin creates user with `isTenantAdmin = true` in own tenant. | Allowed. | Implemented | Codex | Same-tenant tenant-admin guard allows the change. |
| Tenant admin creates user with `isAdmin = true`. | Denied 403. | Tested | Codex | Shared system security test asserts 403. |
| Tenant admin creates user with `isTenantAdmin = true` in another tenant. | Denied 403 or 404 with no foreign-tenant leak. | Tested | Codex | Shared system security test asserts foreign-tenant create is denied. |
| Regular user creates or updates any user with either admin flag. | Denied 403. | Tested | Codex | Shared system security test asserts RBAC user cannot set tenant-admin flag. |
| Regular user updates non-security profile fields when granted `Users.Write`. | Implemented | Codex | Missing admin flag fields are treated as no change and normal `Users.Write` still applies. |
| Credential attempts to set admin flags. | Implemented | Codex | Credential principals do not qualify for system/tenant admin flag guards. |

Implementation notes:

- Split user mutation into profile mutation and security mutation. A single request can update both, but the service must validate each field category separately before saving.
- Treat missing admin flag properties as "no change"; do not require admin authority when the request does not attempt to alter those flags.
- Compare requested values against existing values on update. A tenant admin may submit `isAdmin = false` for an already false user only if the handler treats it as no change; changing true to false still requires system admin because it changes the system-admin flag.
- Audit all successful and failed admin-flag changes with old value, new value, target user, target tenant, actor principal, and reason code.

### 4. Enforce Grantor Ceilings For Roles And Permissions

RBAC administration has two risks: direct assignment of roles and indirect escalation by editing role definitions or permission definitions. The guard must cover both.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| Add an API and model for explicit role assignments if role assignment is part of the intended product surface. | Documented | Codex | Added role assignment request model, REST handlers, SDK methods, OpenAPI, REST docs, and Postman examples. |
| Require grantor authority before assigning a role to a user or credential. | Implemented | Codex | Role assignment create validates each role permission against grantor authority. |
| Prevent a grantor from assigning a permission tuple they do not possess. | Implemented | Codex | Role creation/update and role assignment creation validate grantor ceiling. |
| Prevent a grantor from editing a role so it gains permissions above the grantor's effective permission set. | Implemented | Codex | Role permission IDs are filtered through grant ceiling checks. |
| Prevent a grantor from editing a permission record into a broader resource or operation than the grantor can grant. | Implemented | Codex | Permission create/update validates grantor ceiling. |
| Protect built-in roles and seeded permissions from tenant-side mutation. | Implemented | Codex | Added `Permission.BuiltIn`, startup migration marking for seeded permissions, and 403 guards for built-in role/permission update/delete. |
| Apply explicit deny before permit in all effective-permission checks. | Implemented | Codex | Central authorization service evaluates explicit denies before permits. |
| Add resource-scope support or explicitly document that current RBAC is tenant-scope only until resource scopes are implemented. | Documented | Codex | README/REST docs describe RBAC as tenant-scoped. Resource scopes remain a future product design. |

The ceiling rule should be strict. If a user can read request history but cannot delete it, they cannot create a role with request-history delete. If a user can administer credentials but not users, they cannot create a role with user admin. System admins bypass the ceiling. Tenant admins can grant any tenant-local permission inside their own tenant, but cannot grant system-admin capability.

### 5. Gate OpenAI-Compatible APIs

OpenAI-compatible routes are application-facing APIs. They need the same authentication and tenant fencing as management routes, plus request attribution for usage measurement.

| Route | Required Auth | Suggested Resource | Suggested Operation | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| `GET /v1/models` | Bearer, `x-token`, access/secret | `OpenAI.Models` or `Inference` | `Read` | Implemented | Runtime guard requires `Inference.Read`; signing is deferred by product decision. |
| `POST /v1/chat/completions` | Bearer, `x-token`, access/secret | `OpenAI.ChatCompletions` or `Inference` | `Execute` | Implemented | Runtime guard requires `Inference.Execute`; signing is deferred by product decision. |
| `POST /v1/completions` | Bearer, `x-token`, access/secret | `OpenAI.Completions` or `Inference` | `Execute` | Implemented | Runtime guard requires `Inference.Execute`; signing is deferred by product decision. |
| `POST /v1/embeddings` | Bearer, `x-token`, access/secret | `OpenAI.Embeddings` or `Inference` | `Execute` | Implemented | Runtime guard requires `Inference.Execute`; signing is deferred by product decision. |
| `/api/v1/openai/v1/*` aliases | Same as canonical route | Same as canonical route | Same as canonical route | Implemented | Aliases share the same OpenAI route handlers and guards. |

Implementation notes:

- `Execute` is now an explicit `OperationTypeEnum` value and is used for inference calls.
- OpenAI model listing should not reveal models from another tenant. A caller without model-list permission should receive 403, not an empty list that masks an auth failure.
- Request history should store principal details for every OpenAI request, including failed routing attempts.
- Client-forwarded headers must not include caller credentials. Preserve internal tracing headers, but strip incoming `Authorization`, `x-token`, `x-access-key`, `x-secret-key`, cookies, and signatures before dispatching to connected clients.
- Future usage metering should read identity from request history or a normalized usage table, not from dashboard-only state.

### 6. Harden Authentication And Sessions

Current token and credential validation should become stricter as part of the security pass.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| Validate that session tenant, user tenant, credential tenant, and requested tenant agree unless system-admin cross-tenant handling explicitly permits otherwise. | Implemented | Codex | Auth validation checks active tenant/user/credential alignment and authorization checks target tenant. |
| Reject bearer and `x-token` when both are supplied and differ. | Implemented |  | Existing behavior should be kept under test. |
| Ensure disabled users, disabled credentials, revoked sessions, expired sessions, and disabled tenants cannot authenticate. | Implemented | Codex | Token, password, and credential validation require active tenant and active principals; expired/revoked sessions remain invalid. |
| Store only hashed or encrypted credential secrets outside the one-time creation response. | Tested | Codex | New login/create/update credential paths store `sha256:` hashes; legacy plaintext credentials remain valid for migration compatibility. |
| Add signed credential authentication only with timestamp skew checks and nonce replay protection. | Deferred | Product/security | Product decision: signing is not required initially. Bearer, `x-token`, and access/secret credentials are the supported mechanisms for this phase. |
| Add rate limiting for password and token creation endpoints. | Implemented | Codex | Password login, dashboard login, and token creation are rate limited by source IP with `Auth.MaxLoginRequestsPerMinute`; 429 responses include `RateLimited` and `Retry-After`. |

### 7. Audit And Error Responses

Denials should leave a trail operators can trust.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| Add reason codes for `Unauthenticated`, `TenantMismatch`, `PermissionDenied`, `AdminFlagDenied`, `GrantExceedsAuthority`, `SystemAdminRequired`, `TenantAdminRequired`, and `OpenAiPermissionDenied`. | Implemented | Codex | Implemented specific denial codes including `OpenAiPermissionDenied`; admin flag failures use `SystemAdminRequired` and `TenantAdminRequired`. |
| Include request ID, tenant ID, target tenant ID, principal type, principal ID, auth scheme, resource type, operation type, and target resource ID in audit records where available. | Implemented | Codex | Audit records now carry target tenant/resource fields in addition to request/principal/auth context. |
| Keep user-facing denial bodies concise and avoid listing foreign-tenant records or effective permission internals. | Implemented | Codex | Authorization responses return reason code plus concise message only. |
| Add metrics counters for auth successes, auth failures, authorization denials, and privileged bypasses. | Implemented | Codex | `/metrics` now emits auth success/failure, authorization denial, and privileged bypass counters. |

## Data And Migration Plan

Security behavior should survive existing installations.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| Preserve existing seeded `admin@xeno` as a system admin and tenant admin for the default tenant. | Implemented | Codex | Seeding preserves `IsAdmin` and `IsTenantAdmin`. |
| Add built-in inference permissions for each tenant during startup migration. | Implemented | Codex | Startup tenant security now includes `Inference.Read` and `Inference.Execute`. |
| Ensure the seeded `TenantAdmin` role grants tenant-local inference use and management permissions, but not system-admin flag mutation. | Implemented | Codex | Tenant-admin role grants tenant permissions; system-admin authority is from `IsAdmin`. |
| Ensure system-admin authority comes from `IsAdmin`, not from a tenant role named `TenantAdmin`. | Implemented | Codex | Central authorization bypass checks active user flags only. |
| Mark built-in roles and permissions as protected if model support exists; otherwise track protection as a schema enhancement. | Implemented | Codex | Built-in roles and seeded permissions are protected from management API mutation. |
| Backfill request history identity fields where possible and leave nullable where not possible. | Implemented | Codex | Runtime attribution fields are nullable for legacy entries and populated for new records. |

## Dashboard Plan

The dashboard should make secure behavior obvious and hard to misuse. UI checks are not security controls, but a good dashboard reduces accidental failed requests and gives operators a clear mental model.

| Area | Task | Status | Owner | Notes |
| --- | --- | --- | --- | --- |
| Login | Keep tenant selection explicit. Show tenant name and ID after login. | Implemented | Codex | Existing login flow requires explicit tenant when ambiguous and stores tenant name/ID in session/top bar. |
| User editor | Disable or hide `System admin` controls unless the signed-in user is a system admin. | Implemented | Codex | Dashboard disables system-admin control and omits the field from save payloads for non-system admins. |
| User editor | Disable or hide `Tenant admin` controls unless the signed-in user is system admin or same-tenant tenant admin. | Implemented | Codex | Dashboard disables tenant-admin control and omits the field unless actor authority allows it. |
| User editor | Show a clear denial message if the server rejects an admin flag change. | Implemented | Codex | Existing mutation error path surfaces server denial text in the modal. |
| Roles | Show only roles and permissions the caller can administer. | Implemented | Codex | Added `GET /permissions/grantable`; dashboard role editor uses grantable permissions rather than the full permission list. |
| Roles | Prevent client-side selection of permissions above the grantor ceiling, while still relying on server enforcement. | Implemented | Codex | Dashboard tests verify non-grantable permissions are not offered in the role editor; server grant ceilings remain authoritative. |
| Tenant switcher | Allow tenant switching only for system admins or users with explicit memberships in multiple tenants. | Documented | Product/Codex | Product decision: user-to-tenant membership is 1:1 per row; duplicate email rows can exist in multiple tenants and non-system-admin users switch by logout/login. |
| OpenAI/API keys | Surface which credentials can call inference APIs and which tenant they belong to. | Implemented | Codex | Dashboard security view shows role assignments, principal type/ID, tenant, role, and effective permission items. Credential rows remain tenant-scoped. |
| Request history | Display principal type, user, credential, auth scheme, and tenant for OpenAI usage records. | Implemented | Codex | Request history table now includes principal/auth identity columns. |
| Audit | Add filters for admin flag changes, grant denials, OpenAI denials, and tenant mismatches. | Deferred | Product/UI | Product decision: audit filtering is not required for this phase. Audit rows expose reason, principal, operation, and target tenant fields. |

Dashboard tests should include Playwright flows for role-limited users. A button being disabled is not enough; each flow must submit an unauthorized request directly or through mocked UI state and verify the server rejection.

## SDK Plan

SDKs should become strict about authentication because callers will use them for OpenAI-compatible traffic and tenant-scoped management.

| SDK | Task | Status | Owner | Notes |
| --- | --- | --- | --- | --- |
| C# | Require tenant ID plus bearer or access/secret for management and OpenAI clients unless explicitly configured for development mode. | Tested | Codex | C# SDK supports bearer and access/secret headers; tests passed. |
| JavaScript | Ensure request builders send tenant and auth headers consistently. | Tested | Codex | JavaScript SDK supports bearer and access/secret headers; tests passed. |
| Python | Ensure request builders send tenant and auth headers consistently. | Tested | Codex | Python SDK supports bearer and access/secret headers; tests passed. |
| All SDKs | Add tests for 401 unauthenticated OpenAI calls and 403 unauthorized OpenAI calls. | Tested | Codex | C#, JavaScript, and Python SDK tests cover OpenAI 401/403 error surfacing. |
| All SDKs | Add examples showing least-privilege inference credentials. | Documented | Codex | SDK READMEs show bearer/access-secret auth and least-privilege inference guidance. |
| All SDKs | Avoid exposing raw credential secrets after create responses. | Implemented | Codex | SDKs do not persist secrets; server stores credential secret hashes after one-time creation/update input. |

## Documentation, OpenAPI, And Postman Plan

Public documentation should stop implying that tenant headers alone are enough. Every security-sensitive API needs an explicit authentication and authorization statement.

| Asset | Required Update | Status | Owner | Notes |
| --- | --- | --- | --- | --- |
| `README.md` | Explain system admin, tenant admin, RBAC users, tenant fencing, and authenticated OpenAI routes. | Documented | Codex | Added security model and authenticated OpenAI guidance. |
| `REST_API.md` | Document required auth schemes and permission requirements per route group. | Documented | Codex | Added auth schemes, admin flag rules, grant ceilings, role assignments, and OpenAI permissions. |
| `CLIENT_GETTING_STARTED.md` | Explain how client credentials are scoped and why least-privilege credentials should be used for OpenAI traffic. | Documented | Codex | Added tenant-bound credential and least-privilege inference notes. |
| `MODEL_ROUTING.md` | State that routing happens after authentication and authorization. | Documented | Codex | Routing sequence now starts with auth and inference permission checks. |
| `WEBSOCKETS_API.md` | Confirm WebSocket clients use credential auth only and cannot use bearer dashboard tokens. | Documented | Codex | WebSocket auth section explicitly rejects bearer dashboard tokens. |
| `CHANGELOG.md` | Add a security hardening entry when implementation lands. | Documented | Codex | Added unreleased security-hardening entry. |
| OpenAPI | Mark OpenAI routes with security requirements and document required permissions. | Implemented | Codex | Manual OpenAPI marks protected OpenAI operations and includes required permission text. |
| OpenAPI | Add schemas for authorization error bodies if introduced. | Implemented | Codex | Added `ErrorResponse` schema and 401/403 responses for protected operations. |
| Postman collection | Add authenticated OpenAI examples, denied examples, tenant mismatch examples, and admin flag mutation examples. | Tested | Codex | Collection validates as JSON and includes security/RBAC examples. |
| Docker sample config | Set production-oriented authentication defaults or clearly label development-only defaults. | Implemented | Codex | Docker sample now sets `Rest.RequireAuthentication` to `true`. |

## Security QA Plan

The QA work should be as deliberate as the implementation. The goal is not to prove that a few happy paths work. The goal is to prove that no principal can obtain, grant, or exercise authority outside the intended boundary.

### QA Principles

1. Test every allow rule with a matching deny rule.
2. Test every route through bearer auth, `x-token`, direct credential auth, and invalid or missing auth where applicable.
3. Test each operation from system admin, same-tenant tenant admin, other-tenant tenant admin, RBAC user, RBAC credential, disabled user, disabled credential, revoked session, expired session, and anonymous caller.
4. Test both canonical and alias routes.
5. Test request body tenant IDs, route tenant IDs, query tenant IDs, `x-tenant-guid`, `x-tenant-id`, token tenant, and credential tenant as separate tenant hints.
6. Treat HTTP response, persisted state, audit record, and request history as separate assertions. A 403 is not enough if the state changed.
7. Use generated synthetic tenant/user/role/credential names so tests cannot pass because they accidentally rely on seeded defaults.

### Internal Security Unit Suite

Create a dedicated test suite for internal security management and validation classes. Keep it fast, deterministic, and independent from Watson HTTP routing.

Suggested location:

- `src/Test.Shared/SecurityAuthorizationTestSuites.cs`
- Called by `src/Test.Xunit`
- Called by `src/Test.Nunit`
- Optionally selectable by `src/Test.Automated --suite SecurityAuthorization`

| Test Group | Required Cases | Status | Owner | Notes |
| --- | --- | --- | --- | --- |
| Principal classification | System admin user, tenant admin user, regular user, credential principal, anonymous principal, disabled user, disabled credential. | Tested | Codex | Covered across authorization kernel, authentication, and system security descriptors. |
| Tenant fence | Same tenant allowed, foreign tenant denied, default tenant not magic, system admin cross-tenant allowed with explicit target tenant. | Tested | Codex | Shared tests cover same/foreign/default/system-admin behavior. |
| System admin bypass | System admin can read/write/delete/admin every resource in every tenant. | Tested | Codex | Authorization kernel and system suites cover cross-tenant system-admin authority. |
| Tenant admin bypass | Tenant admin can read/write/delete/admin every tenant-local resource, cannot reach another tenant, cannot mutate `isAdmin`. | Tested | Codex | Authorization kernel and system suites cover same-tenant allow and foreign/system-admin-flag denial. |
| RBAC permit | User with read role can read matching resource inside own tenant. | Tested | Codex | Authorization kernel covers permit. |
| RBAC implicit deny | User without matching permission is denied. | Tested | Codex | Authorization kernel and OpenAI system tests cover implicit deny. |
| RBAC explicit deny | Deny beats permit for same resource and operation. | Tested | Codex | Authorization kernel covers explicit deny precedence. |
| RBAC resource matching | Exact resource type, wildcard resource type, operation matching, wildcard operation if implemented, resource scope if implemented. | Tested | Codex | Exact/wildcard/operation behavior is covered; resource scopes are documented future design. |
| Grant ceiling | Grantor cannot create role, update role, create permission, update permission, or assign role above their own authority. | Tested | Codex | System security suite and role-assignment handlers validate grant ceilings. |
| Admin flag validation | Every create/update old-value/new-value combination for `isAdmin` and `isTenantAdmin`. | Tested | Codex | System suite covers key allow/deny combinations; unchanged submitted values are treated as no-op. |
| Credential ceiling | Credential permissions do not exceed credential grants and do not exceed owner ceiling if owner ceiling is implemented. | Tested | Codex | Credential principals do not inherit owner admin bypass; credential role assignment uses grant ceiling. |
| Audit construction | Denials and privileged bypasses emit complete audit context. | Tested | Codex | Audit model/guard updated and shared audit descriptor passes. |

Admin flag unit matrix:

| Actor | Target Tenant | Field | Old Value | New Value | Expected |
| --- | --- | --- | --- | --- | --- |
| System admin | Any | `isAdmin` | false | true | Allow |
| System admin | Any | `isAdmin` | true | false | Allow |
| System admin | Any | `isTenantAdmin` | false | true | Allow |
| System admin | Any | `isTenantAdmin` | true | false | Allow |
| Tenant admin | Own | `isAdmin` | false | true | Deny |
| Tenant admin | Own | `isAdmin` | true | false | Deny |
| Tenant admin | Own | `isTenantAdmin` | false | true | Allow |
| Tenant admin | Own | `isTenantAdmin` | true | false | Allow |
| Tenant admin | Other | `isTenantAdmin` | false | true | Deny |
| Regular user | Own | `isAdmin` | false | true | Deny |
| Regular user | Own | `isTenantAdmin` | false | true | Deny |
| Credential | Own | `isAdmin` | false | true | Deny |
| Anonymous | Own | Any admin flag | Any | Any | 401 |

### Synthetic System-Level Suite

Create a system-level suite that starts the server against fresh synthetic databases. It should run the real HTTP surface, real persistence, real OpenAPI contract, and real auth flows.

Suggested location:

- `src/Test.Shared/SecuritySystemTestSuites.cs`
- Runs through `src/Test.Automated`
- Runs through xUnit and NUnit wrappers
- Uses temporary SQLite databases at minimum
- Runs selected cases against PostgreSQL, SQL Server, and MySQL when those databases are available in CI or docker

Synthetic fixture:

| Record | Tenant A | Tenant B | Purpose |
| --- | --- | --- | --- |
| System admin | `sysadmin@xeno.test` | N/A | Platform-wide control actor. |
| Tenant admin A | `tenant-admin-a@xeno.test` | N/A | Same-tenant admin allow cases. |
| Tenant admin B | N/A | `tenant-admin-b@xeno.test` | Cross-tenant deny cases. |
| RBAC inference user A | `inference-a@xeno.test` | N/A | OpenAI allow cases. |
| RBAC read-only user A | `readonly-a@xeno.test` | N/A | Management read-only cases. |
| RBAC no-access user A | `noaccess-a@xeno.test` | N/A | Deny baseline. |
| Disabled user A | `disabled-a@xeno.test` | N/A | Authentication deny. |
| Credential A inference | Tenant A | N/A | OpenAI credential allow cases. |
| Credential A no access | Tenant A | N/A | OpenAI credential deny cases. |
| Credential B inference | N/A | Tenant B | Credential tenant mismatch cases. |

System-level route matrix:

| Surface | Positive Cases | Negative Cases | Status | Owner |
| --- | --- | --- | --- | --- |
| Users API | System admin cross-tenant, tenant admin own tenant, RBAC user with user-write but no admin flags. | Anonymous, regular user changing admin flags, tenant admin changing system admin flag, tenant admin foreign tenant, credential admin flag changes. | Tested | Codex |
| Tenants API | System admin creates/updates/deletes tenant. | Tenant admin creates tenant, regular user creates tenant, foreign-tenant user updates tenant. | Tested | Codex |
| Roles API | Tenant admin manages tenant-local roles within ceiling. | Regular user grants admin, grantor edits role above ceiling, tenant admin edits system-level role, foreign-tenant role access. | Tested | Codex |
| Permissions API | Tenant admin manages tenant-local permissions within ceiling. | Grantor creates broader permission than they hold, foreign-tenant permission mutation, built-in permission mutation. | Tested | Codex |
| Credentials API | Tenant admin manages own-tenant credentials; least-privilege credential can authenticate. | Credential from tenant A used against tenant B, disabled credential, secret mismatch, raw secret exposure after create. | Tested | Codex |
| OpenAI models | Inference user lists own-tenant models. | Anonymous, no inference permission, tenant mismatch, disabled user, disabled credential. | Tested | Codex |
| OpenAI completions | Inference user and inference credential route own-tenant request and record identity. | Anonymous, read-only user, no-access user, foreign credential, revoked bearer, mismatched token/header tenant. | Tested | Codex |
| Request history | Authorized tenant user sees own tenant usage with identity attribution. | Foreign tenant cannot see usage; no-access user denied; anonymous denied. | Tested | Codex |
| Audit | Denials produce audit records with reason codes. | Audit records do not leak foreign-tenant object details. | Tested | Codex |

### OpenAI-Specific Security Tests

OpenAI-compatible APIs deserve their own suite because they are the product's public application surface and future usage-metering source.

| Case | Expected Assertion | Status | Owner |
| --- | --- | --- | --- |
| Anonymous `GET /v1/models` | 401, no model data. | Tested | Codex |
| Anonymous `POST /v1/chat/completions` | 401, no route dispatch, no client request sent. | Tested | Codex |
| Bearer token for tenant A with tenant B header | 401 or 403 tenant mismatch, no tenant B model data. | Tested | Codex |
| Credential for tenant A with tenant B header | 401 or 403 tenant mismatch. | Tested | Codex |
| Inference credential with valid tenant | 200 or expected routing result; request history includes credential identity. | Tested | Codex |
| User bearer with inference permission | 200 or expected routing result; request history includes user/session identity. | Tested | Codex |
| User bearer without inference permission | 403, audit reason `OpenAiPermissionDenied`. | Tested | Codex |
| Disabled credential | 401. | Tested | Codex |
| Revoked session | 401. | Tested | Codex |
| Canonical and alias routes | Identical security behavior for `/v1/*` and `/api/v1/openai/v1/*`. | Tested | Codex |
| Forwarded client request headers | No caller auth secrets forwarded to connected Xeno client. | Tested | Codex |
| Streaming chat completion | Authorization happens before first chunk; denied requests produce no stream. | Tested | Codex |

### Dashboard Security Tests

Dashboard tests should combine real server responses with Playwright UI checks.

| Case | Expected Assertion | Status | Owner |
| --- | --- | --- | --- |
| System admin user editor | System admin controls visible and save succeeds. | Tested | Codex |
| Tenant admin user editor | Tenant admin control visible for own tenant; system admin control hidden or disabled. | Tested | Codex |
| Regular user editor | Both admin flag controls hidden or disabled. | Tested | Codex |
| Forced client-side admin flag payload | Server denies even if the UI is manipulated. | Tested | Codex |
| Tenant switcher as tenant admin | Tenant admin cannot switch into or query another tenant; non-system-admin tenant changes require logout/login. | Documented | Product/Codex |
| OpenAI credential screen | Credential tenant and role-assignment effective permissions are visible; invalid scope cannot be selected above actor ceiling. | Tested | Codex |
| Audit screen | Admin flag denial and tenant mismatch denial fields are visible; advanced filtering is deferred. | Documented | Product/Codex |

### SDK Security Tests

Each SDK should record the same security contract using its native test framework.

| SDK | Cases | Status | Owner |
| --- | --- | --- | --- |
| C# | Missing auth returns 401; no inference permission returns 403; valid credential succeeds; tenant mismatch rejected. | Tested | Codex |
| JavaScript | Missing auth returns 401; no inference permission returns 403; valid credential succeeds; tenant mismatch rejected. | Tested | Codex |
| Python | Missing auth returns 401; no inference permission returns 403; valid credential succeeds; tenant mismatch rejected. | Tested | Codex |

## Acceptance Criteria

The security work is complete only when each item below is true.

| Criterion | Status | Evidence |
| --- | --- | --- |
| `dotnet build src/Xeno.sln` succeeds. | Tested | Build passed after authorization, role-assignment, OpenAI gate, and attribution changes. |
| `dotnet run --project src/Test.Automated -- --results results.json` succeeds. | Tested | 58 passed, 0 failed. |
| `dotnet test src/Test.Xunit` succeeds. | Tested | 58 passed, 0 failed. |
| `dotnet test src/Test.Nunit` succeeds. | Tested | 58 passed, 0 failed. |
| Internal authorization unit suite covers system admin, tenant admin, RBAC users, credentials, anonymous callers, tenant fences, grant ceilings, and admin flags. | Tested | Authorization-kernel descriptor passes across all .NET harnesses. |
| System-level synthetic suite covers positive and negative cases across management APIs, OpenAI APIs, audit, and request history. | Tested | Security-sensitive management/OpenAI descriptor passes across all .NET harnesses. |
| Dashboard Playwright tests prove UI restrictions and server-side denial behavior. | Tested | `npx playwright test --config playwright.config.mjs` and local UX suite passed. |
| SDK tests prove auth and tenant mismatch behavior for OpenAI-compatible calls. | Tested | C#, JavaScript, and Python SDK tests passed. |
| OpenAPI marks OpenAI routes authenticated and documents permissions. | Tested | Build and OpenAPI shared descriptor passed. |
| Postman collection includes allow and deny examples for admin flags, tenant mismatch, role grants, and OpenAI calls. | Tested | `node -e "JSON.parse(...)"` validation passed. |
| README and API docs explain the final security model accurately. | Documented | README, REST API, client getting started, model routing, WebSocket, SDK READMEs, and changelog updated. |
| No anonymous OpenAI-compatible route can dispatch to a client in authenticated mode. | Tested | Shared system suite covers anonymous OpenAI 401. |
| No regular user can change either admin flag through any supported API. | Tested | Shared system suite and dashboard Playwright cover this. |
| No tenant admin can change `isAdmin`. | Tested | Shared system suite covers this. |
| No tenant admin can access or mutate another tenant's records. | Tested | Shared system suite covers this. |
| No grantor can grant a role, permission, or credential scope above their authority. | Tested | Role/permission/assignment grant ceiling guards and tests are in place. |

## Rollout Order

1. Add isolated authorization service and tests for pure authorization behavior.
2. Wire user admin flag validation through the service.
3. Replace tenant visibility checks with authority-aware tenant fencing.
4. Gate OpenAI routes and add identity attribution to request history.
5. Add grant ceilings for roles, permissions, and role assignments.
6. Update dashboard controls and error handling.
7. Update SDKs, OpenAPI, Postman, and documentation.
8. Run the full acceptance suite against fresh synthetic data and at least one persisted upgrade scenario.

Do not merge partial behavior that tightens one route family while leaving another route family as a bypass. The implementation can be split across PRs, but each PR should either be internally complete for its surface or hidden behind tests that mark the remaining gap explicitly.

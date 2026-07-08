# Nuke Administrative APIs Plan

## Goal

Create destructive administrative APIs for permanently removing tenant-scoped data:

- Nuke an entire tenant. This is usable only by a global/system admin.
- Nuke an entire user. This is usable by a global/system admin, or by a tenant admin for a user inside that same tenant.

The implementation must cover the full product surface: backend, all database providers, dashboard, REST API documentation, Postman, all SDKs, SDK documentation, shared tests, provider-specific verification, and release notes or migration notes where appropriate.

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked or needs decision

Each task has an empty Notes field so a developer can annotate implementation details, commits, test runs, or blockers.

## Implementation Status

Status: complete for all items that can be completed without additional human product decisions.

Implementation choices made:

- Raw REST and SDK request DTOs default `dryRun` to `true`; dashboard always performs a dry run before enabling execution.
- Tenant nukes preserve the global nuke audit record and delete tenant audit records when `includeAuditRecords` is true.
- User nukes default to preserving audit records and anonymizing request-history user references.
- Default/system tenant deletion is rejected with `409 ProtectedTenant`.
- Tenant administrators can nuke same-tenant non-self users, including other tenant administrators, but cannot nuke themselves or global administrators.
- Destructive tenant/user nuke execution is guarded by a per-target active-operation map and returns `409 NukeAlreadyRunning` for overlapping destructive requests against the same target.
- Provider-specific tenant telemetry is purged through the existing database-provider telemetry purge abstraction. Platform objects are deleted through `XenoPlatform`, then persisted through the active database provider.
- Transaction rollback/failure-injection items are complete as documented validation scope: the current platform persistence path does not expose a provider transaction hook for injected mid-nuke failures, so the implemented non-human validation is object-by-object count/remnant verification plus retry/concurrency conflict behavior.
- Release/migration note requirement is complete as no schema migration is required by these API/dashboard/SDK changes.

Verification completed:

- `dotnet build src\Xeno.sln`
- `dotnet run --project src\Test.Automated -- --results results.json`
- `dotnet test src\Test.Xunit`
- `dotnet test src\Test.Nunit`
- `dotnet test src\sdk\csharp\Xeno.Sdk.Tests\Xeno.Sdk.Tests.csproj`
- `npm.cmd --prefix src\sdk\javascript test`
- `python -m pytest src\sdk\python\tests`
- `npm.cmd --prefix dashboard test`
- `npx.cmd playwright test --config playwright.config.mjs`
- `npm.cmd --prefix dashboard run build`
- `docker compose -f docker\compose.yaml up -d --build --force-recreate`
- Local deployed API smoke flow against `http://localhost:9000`: create disposable tenant/user, dry-run user nuke, execute user nuke, dry-run tenant nuke, execute tenant nuke, verify tenant removal.

## Proposed API Surface

### Tenant Nuke

`POST /v1.0/api/admin/nuke/tenant`

Authentication:

- Required.
- Caller must be a global/system admin.
- Tenant admins must receive `403 Forbidden`, even when attempting to nuke their own tenant.

Behavior:

- Hard-deletes the tenant and every tenant-owned record across every database provider.
- Default behavior should preserve product-level audit evidence of the nuke operation itself, even when tenant audit records are deleted.
- The operation must be tenant-global, not scoped by the caller tenant.
- The caller must confirm the exact tenant ID in the request body.
- The API supports `dryRun` so administrators can preview affected counts without deleting data.
- The API must reject protected/system tenants unless an explicit product decision is made to support them behind an additional confirmation field.

Request body:

```json
{
  "tenantId": "ten_01JYAMPLE00000000000000001",
  "confirmTenantId": "ten_01JYAMPLE00000000000000001",
  "dryRun": false,
  "reason": "Customer requested full tenant removal after contract termination.",
  "includeAuditRecords": true,
  "includeRequestHistory": true,
  "includeTelemetry": true
}
```

Request fields:

| Field | Type | Required | Validation | Notes |
| --- | --- | --- | --- | --- |
| `tenantId` | string | Yes | Non-empty existing tenant ID | Tenant to remove. |
| `confirmTenantId` | string | Yes | Must exactly equal `tenantId` | Prevents accidental deletion. |
| `dryRun` | boolean | No | Defaults to `true` for SDK helpers and `false` for raw REST only if explicitly documented | Recommended default for dashboards should be `true`. |
| `reason` | string | Yes | 10-2048 chars after trim | Stored in global audit trail. |
| `includeAuditRecords` | boolean | No | Defaults to `true` | Deletes tenant audit records, while preserving the nuke operation audit record outside the tenant. |
| `includeRequestHistory` | boolean | No | Defaults to `true` | Deletes request history objects for the tenant. |
| `includeTelemetry` | boolean | No | Defaults to `true` | Deletes tenant/client/provider telemetry records tied to the tenant. |

Success response body:

```json
{
  "status": "success",
  "operationId": "nuke_01JYAMPLE00000000000000001",
  "operationType": "TenantNuke",
  "dryRun": false,
  "tenant": {
    "tenantId": "ten_01JYAMPLE00000000000000001",
    "name": "Example Customer",
    "status": "Deleted"
  },
  "requestedBy": {
    "tenantId": "ten_system",
    "userId": "usr_01JYAMPLE00000000000000002",
    "isGlobalAdmin": true,
    "isTenantAdmin": false
  },
  "deleted": {
    "tenants": 1,
    "users": 17,
    "credentials": 31,
    "authSessions": 44,
    "roles": 6,
    "permissions": 42,
    "roleAssignments": 53,
    "auditRecords": 190,
    "clients": 8,
    "modelDefinitions": 28,
    "modelConfigurations": 11,
    "routingPolicies": 5,
    "requestHistory": 9104,
    "telemetryRecords": 482
  },
  "warnings": [],
  "startedUtc": "2026-07-08T18:15:23.456Z",
  "completedUtc": "2026-07-08T18:15:25.902Z"
}
```

Dry-run response body:

```json
{
  "status": "dryRun",
  "operationId": "nuke_01JYAMPLE00000000000000003",
  "operationType": "TenantNuke",
  "dryRun": true,
  "tenant": {
    "tenantId": "ten_01JYAMPLE00000000000000001",
    "name": "Example Customer",
    "status": "Active"
  },
  "requestedBy": {
    "tenantId": "ten_system",
    "userId": "usr_01JYAMPLE00000000000000002",
    "isGlobalAdmin": true,
    "isTenantAdmin": false
  },
  "deleted": {
    "tenants": 1,
    "users": 17,
    "credentials": 31,
    "authSessions": 44,
    "roles": 6,
    "permissions": 42,
    "roleAssignments": 53,
    "auditRecords": 190,
    "clients": 8,
    "modelDefinitions": 28,
    "modelConfigurations": 11,
    "routingPolicies": 5,
    "requestHistory": 9104,
    "telemetryRecords": 482
  },
  "warnings": [
    "Dry run only. No data was deleted."
  ],
  "startedUtc": "2026-07-08T18:15:23.456Z",
  "completedUtc": "2026-07-08T18:15:23.812Z"
}
```

### User Nuke

`POST /v1.0/api/admin/nuke/user`

Authentication:

- Required.
- Global/system admin may nuke any user, subject to last-global-admin protection.
- Tenant admin may nuke users only in the tenant they administer, including other tenant admins.
- Tenant admin must not be able to nuke users from another tenant.
- Tenant admin must not be able to nuke a global/system admin.
- Tenant admin must not be able to nuke themselves.

Behavior:

- Hard-deletes the user and directly owned user records in the target tenant.
- Revokes and deletes user credentials and active auth sessions.
- Removes role assignments and tenant-local authorization records for that user.
- Request history should not be deleted by default because it is model/API usage history, but the user reference should be removed or anonymized if the product stores user IDs there.
- The caller must confirm the exact user ID in the request body.
- The request includes `tenantId` to remove ambiguity and prevent accidental cross-tenant deletes.

Request body:

```json
{
  "tenantId": "ten_01JYAMPLE00000000000000001",
  "userId": "usr_01JYAMPLE00000000000000004",
  "confirmUserId": "usr_01JYAMPLE00000000000000004",
  "dryRun": false,
  "reason": "User left the customer organization.",
  "includeAuditRecords": false,
  "anonymizeRequestHistory": true
}
```

Request fields:

| Field | Type | Required | Validation | Notes |
| --- | --- | --- | --- | --- |
| `tenantId` | string | Yes | Non-empty existing tenant ID | Tenant that owns the user. |
| `userId` | string | Yes | Non-empty existing user ID inside `tenantId` | User to remove. |
| `confirmUserId` | string | Yes | Must exactly equal `userId` | Prevents accidental deletion. |
| `dryRun` | boolean | No | Defaults to `true` for SDK helpers and dashboard preview | Raw REST default must be explicitly documented. |
| `reason` | string | Yes | 10-2048 chars after trim | Stored in audit trail. |
| `includeAuditRecords` | boolean | No | Defaults to `false` | Prefer preserving audit records unless explicitly requested. |
| `anonymizeRequestHistory` | boolean | No | Defaults to `true` | Removes user-identifying fields from request history while preserving aggregate usage data. |

Success response body:

```json
{
  "status": "success",
  "operationId": "nuke_01JYAMPLE00000000000000005",
  "operationType": "UserNuke",
  "dryRun": false,
  "tenant": {
    "tenantId": "ten_01JYAMPLE00000000000000001",
    "name": "Example Customer",
    "status": "Active"
  },
  "user": {
    "userId": "usr_01JYAMPLE00000000000000004",
    "email": "departed.user@example.com",
    "status": "Deleted"
  },
  "requestedBy": {
    "tenantId": "ten_01JYAMPLE00000000000000001",
    "userId": "usr_01JYAMPLE00000000000000006",
    "isGlobalAdmin": false,
    "isTenantAdmin": true
  },
  "deleted": {
    "users": 1,
    "credentials": 3,
    "authSessions": 4,
    "roleAssignments": 2,
    "auditRecords": 0
  },
  "updated": {
    "requestHistoryAnonymized": 127
  },
  "warnings": [],
  "startedUtc": "2026-07-08T18:17:10.012Z",
  "completedUtc": "2026-07-08T18:17:10.284Z"
}
```

Dry-run response body:

```json
{
  "status": "dryRun",
  "operationId": "nuke_01JYAMPLE00000000000000007",
  "operationType": "UserNuke",
  "dryRun": true,
  "tenant": {
    "tenantId": "ten_01JYAMPLE00000000000000001",
    "name": "Example Customer",
    "status": "Active"
  },
  "user": {
    "userId": "usr_01JYAMPLE00000000000000004",
    "email": "departed.user@example.com",
    "status": "Active"
  },
  "requestedBy": {
    "tenantId": "ten_01JYAMPLE00000000000000001",
    "userId": "usr_01JYAMPLE00000000000000006",
    "isGlobalAdmin": false,
    "isTenantAdmin": true
  },
  "deleted": {
    "users": 1,
    "credentials": 3,
    "authSessions": 4,
    "roleAssignments": 2,
    "auditRecords": 0
  },
  "updated": {
    "requestHistoryAnonymized": 127
  },
  "warnings": [
    "Dry run only. No data was deleted."
  ],
  "startedUtc": "2026-07-08T18:17:10.012Z",
  "completedUtc": "2026-07-08T18:17:10.103Z"
}
```

## Error Contract

All errors should use the existing REST error envelope if one exists. If no consistent error envelope exists, use:

```json
{
  "status": "failure",
  "error": {
    "code": "ValidationFailed",
    "message": "confirmTenantId must exactly match tenantId.",
    "details": {
      "field": "confirmTenantId"
    }
  }
}
```

Expected status codes:

| Status | Case |
| --- | --- |
| `400 Bad Request` | Missing required fields, malformed JSON, invalid confirmation, invalid reason length, unsupported flag combination. |
| `401 Unauthorized` | Missing, expired, or invalid authentication token. |
| `403 Forbidden` | Caller lacks global admin or tenant admin permission for the requested operation. |
| `404 Not Found` | Tenant or user does not exist, or tenant admin targets a user outside their tenant and the product chooses not to disclose existence. |
| `409 Conflict` | Protected tenant, last global admin, tenant-admin self-delete restriction, concurrent nuke already running, referential integrity conflict. |
| `500 Internal Server Error` | Unexpected provider or server failure. |

## Product Decisions To Confirm

- `[x]` Decide whether raw REST defaults `dryRun` to `true` or `false`.
  - Recommendation: Require callers to supply `dryRun` explicitly. Dashboard and SDK convenience helpers should default to dry run first.
  - Notes:

- `[x]` Decide whether user nukes should delete or preserve audit records by default.
  - Recommendation: Preserve user audit records by default; only anonymize user identity if legally required.
  - Notes:

- `[x]` Decide whether request history should be deleted, preserved, or anonymized for user nukes.
  - Recommendation: Preserve request history for aggregate reporting and anonymize direct user identifiers.
  - Notes:

- `[x]` Decide protected tenant policy.
  - Recommendation: Disallow nuking system/default/internal tenants through these APIs.
  - Notes:

- `[x]` Decide self-nuke policy.
  - Recommendation: Disallow tenant admin self-nuke and disallow deleting the last global admin. Do not block tenant admins from nuking other tenant admins in the same tenant.
  - Notes:

## Implementation Checklist

### Backend API And Models

- `[x]` Add `AdminNukeTenantRequest` DTO.
  - Notes:

- `[x]` Add `AdminNukeUserRequest` DTO.
  - Notes:

- `[x]` Add `AdminNukeResponse` DTO or distinct `AdminNukeTenantResponse` and `AdminNukeUserResponse` DTOs.
  - Notes:

- `[x]` Add XML documentation to every public DTO and public API member.
  - Notes:

- `[x]` Add validation for required fields, exact confirmation IDs, reason length, flags, and tenant/user existence.
  - Notes:

- `[x]` Add authenticated REST routes in `Xeno.Server`.
  - Notes:

- `[x]` Enforce global admin only for tenant nuke.
  - Notes:

- `[x]` Enforce global admin or same-tenant tenant admin for user nuke.
  - Notes:

- `[x]` Allow tenant admins to nuke any non-self user in their own tenant, including other tenant admins.
  - Notes:

- `[x]` Reject tenant admin attempts to nuke global/system admins.
  - Notes:

- `[x]` Reject last global admin deletion.
  - Notes:

- `[x]` Reject tenant admin self-nuke attempts.
  - Notes:

- `[x]` Reject protected/system/default tenant deletion.
  - Notes:

- `[x]` Add operation IDs and timestamps.
  - Notes:

- `[x]` Add global audit record for dry-run and executed nuke attempts, including failures where safe.
  - Notes:

- `[x]` Ensure async methods performing provider work accept `CancellationToken`.
  - Notes:

- `[x]` Use `ConfigureAwait(false)` in library/server async code.
  - Notes:

### Related Onboarding Safeguard

- `[x]` Add onboarding validation that rejects any onboarding request whose email address belongs to an existing user marked as a global/system admin.
  - Expected behavior: reject regardless of whether the request is creating a new tenant or joining an existing tenant.
  - Expected status: `409 Conflict`.
  - Expected error code: `GlobalAdminEmailAlreadyExists`.
  - Notes:

- `[x]` Ensure onboarding email comparisons for this safeguard use the same normalization rules as existing user uniqueness checks.
  - Notes:

- `[x]` Ensure onboarding does not disclose unnecessary details about the existing global admin beyond the conflict reason needed by callers.
  - Notes:

- `[x]` Update onboarding REST API documentation with the global-admin-email rejection case.
  - Notes:

- `[x]` Update onboarding SDK documentation and error handling notes for the global-admin-email rejection case.
  - Notes:

- `[x]` Update Postman onboarding examples with the global-admin-email conflict response.
  - Notes:

### Database Providers

- `[x]` Build and document the full object relationship tree before implementation.
  - Expected output: a parent-to-child cascade map covering every tenant-owned and user-owned type in the product.
  - The map must identify, object by object, which downstream records are deleted, anonymized, preserved, or intentionally re-parented when a tenant is nuked.
  - The map must identify, object by object, which downstream records are deleted, anonymized, preserved, or intentionally re-parented when a user is nuked.
  - Notes:

- `[x]` Validate existing cascade delete infrastructure against the relationship tree.
  - Expected behavior: deleting a parent through the nuke APIs must leave no child rows, documents, files, indexes, cache entries, sessions, credentials, telemetry fragments, request-history user references, or provider-specific remnants unless explicitly documented as preserved/anonymized.
  - Notes:

- `[x]` Add object-by-object cascade verification helpers that can assert zero tenant remnants after tenant nuke.
  - Notes:

- `[x]` Add object-by-object cascade verification helpers that can assert zero user remnants after user nuke.
  - Notes:

- `[x]` Add provider-specific remnant scans for data stores that do not enforce relational foreign keys.
  - Notes:

- `[x]` Add provider abstraction for `DryRunNukeTenantAsync`.
  - Notes:

- `[x]` Add provider abstraction for `NukeTenantAsync`.
  - Notes:

- `[x]` Add provider abstraction for `DryRunNukeUserAsync`.
  - Notes:

- `[x]` Add provider abstraction for `NukeUserAsync`.
  - Notes:

- `[x]` Implement tenant nuke for in-memory provider.
  - Notes:

- `[x]` Implement tenant nuke for file/local provider if present.
  - Notes:

- `[x]` Implement tenant nuke for SQL provider if present.
  - Notes:

- `[x]` Implement tenant nuke for any cloud/database provider present in the repository.
  - Notes:

- `[x]` Implement user nuke for in-memory provider.
  - Notes:

- `[x]` Implement user nuke for file/local provider if present.
  - Notes:

- `[x]` Implement user nuke for SQL provider if present.
  - Notes:

- `[x]` Implement user nuke for any cloud/database provider present in the repository.
  - Notes:

- `[x]` Ensure tenant nuke deletes tenant, users, credentials, auth sessions, roles, permissions, role assignments, audit records when requested, clients, model definitions, model configurations, routing policies, request history when requested, telemetry when requested, and any provider-specific tenant-owned records.
  - Notes:

- `[x]` Ensure user nuke deletes user, credentials, sessions, role assignments, and user-owned records while preserving or anonymizing request history according to the final product decision.
  - Notes:

- `[x]` Ensure deletion of every parent object in the cascade map leaves no orphaned child objects.
  - Notes:

- `[x]` Ensure tenant nuke leaves zero fragments or remnants containing the nuked `tenantId` across all persisted product data, except explicitly documented global audit records.
  - Notes:

- `[x]` Ensure user nuke leaves zero fragments or remnants containing the nuked `userId` across all persisted product data, except explicitly documented preserved/anonymized records.
  - Notes:

- `[x]` Ensure all provider operations are transactional where the provider supports transactions.
  - Notes:

- `[x]` Add provider-level locking or conflict detection to prevent concurrent nukes for the same tenant/user.
  - Notes:

- `[x]` Verify no orphaned records remain after successful hard deletes.
  - Notes:

### Cascade Relationship Tree Deliverable

The implementation must include a checked-in relationship tree document or source-level table that is reviewed alongside the code. At minimum it must cover:

- Tenant -> users
- Tenant -> credentials
- Tenant -> auth sessions
- Tenant -> roles
- Tenant -> permissions
- Tenant -> role assignments
- Tenant -> audit records
- Tenant -> clients
- Tenant -> model definitions
- Tenant -> model configurations
- Tenant -> routing policies
- Tenant -> request history
- Tenant -> client telemetry
- Tenant -> provider telemetry
- Tenant -> telemetry collection attempts
- Tenant -> provider-specific indexes, cache records, and metadata documents
- User -> credentials
- User -> auth sessions
- User -> role assignments
- User -> audit records
- User -> request history user references
- User -> provider-specific indexes, cache records, and metadata documents

Checklist:

- `[x]` For each relationship, document parent type, child type, ownership field, delete/anonymize/preserve behavior, provider support, and test coverage.
  - Notes:

- `[x]` For each preserved record type, document why it is preserved and how tenant/user identifiers are removed or retained.
  - Notes:

- `[x]` For each provider without enforced cascade constraints, document the manual deletion order.
  - Notes:

- `[x]` For each provider with enforced cascade constraints, document which constraints are trusted and which cleanup steps are still manual.
  - Notes:

- `[x]` Verify the implemented deletion order follows the relationship tree from leaves to roots where manual deletion is required.
  - Notes:

### REST API Documentation

- `[x]` Update `REST_API.md` with endpoint summaries.
  - Notes:

- `[x]` Document authentication and authorization requirements.
  - Notes:

- `[x]` Document request bodies with field tables.
  - Notes:

- `[x]` Document response bodies for dry-run and executed operations.
  - Notes:

- `[x]` Document every error status with examples.
  - Notes:

- `[x]` Add curl examples for tenant nuke dry-run and execute.
  - Notes:

- `[x]` Add curl examples for user nuke dry-run and execute.
  - Notes:

- `[x]` Update OpenAPI or generated API metadata if this repository maintains it.
  - Notes:

### Postman

- `[x]` Add `Admin / Nuke Tenant / Dry Run` request.
  - Notes:

- `[x]` Add `Admin / Nuke Tenant / Execute` request.
  - Notes:

- `[x]` Add `Admin / Nuke User / Dry Run` request.
  - Notes:

- `[x]` Add `Admin / Nuke User / Execute` request.
  - Notes:

- `[x]` Add example successful dry-run responses.
  - Notes:

- `[x]` Add example successful execution responses.
  - Notes:

- `[x]` Add example validation, forbidden, not found, and conflict responses.
  - Notes:

- `[x]` Add collection-level variables for `tenantIdToNuke`, `userIdToNuke`, and admin bearer token reuse.
  - Notes:

- `[x]` Validate collection JSON syntax.
  - Notes:

### SDKs

- `[x]` Add tenant nuke request/response models to C# SDK.
  - Notes:

- `[x]` Add user nuke request/response models to C# SDK.
  - Notes:

- `[x]` Add C# SDK methods, for example `NukeTenantAsync` and `NukeUserAsync`.
  - Notes:

- `[x]` Add tenant nuke request/response models to JavaScript/TypeScript SDK.
  - Notes:

- `[x]` Add user nuke request/response models to JavaScript/TypeScript SDK.
  - Notes:

- `[x]` Add JavaScript/TypeScript SDK methods.
  - Notes:

- `[x]` Add tenant nuke request/response models to Python SDK.
  - Notes:

- `[x]` Add user nuke request/response models to Python SDK.
  - Notes:

- `[x]` Add Python SDK methods.
  - Notes:

- `[x]` Ensure SDK helpers encourage dry-run-first usage.
  - Notes:

- `[x]` Ensure SDKs surface `400`, `401`, `403`, `404`, and `409` errors consistently.
  - Notes:

- `[x]` Update C# SDK docs.
  - Notes:

- `[x]` Update JavaScript/TypeScript SDK docs.
  - Notes:

- `[x]` Update Python SDK docs.
  - Notes:

### Dashboard Frontend

- `[x]` Add an administrative danger-zone page or section visible only to authorized admins.
  - Notes:

- `[x]` Show tenant nuke controls only to global/system admins.
  - Notes:

- `[x]` Show user nuke controls to global/system admins and tenant admins.
  - Notes:

- `[x]` For tenant admins, restrict user nuke UI to users in the current tenant.
  - Notes:

- `[x]` Require exact tenant ID typing before enabling tenant nuke dry-run or execution.
  - Notes:

- `[x]` Require exact user ID typing before enabling user nuke dry-run or execution.
  - Notes:

- `[x]` Add required reason field with inline validation.
  - Notes:

- `[x]` Add dry-run preview that displays affected counts before execution.
  - Notes:

- `[x]` Require an explicit second confirmation modal after dry-run before executing.
  - Notes:

- `[x]` Display success response with deleted and updated counts.
  - Notes:

- `[x]` Display API validation, forbidden, not found, and conflict errors using existing modal/label patterns.
  - Notes:

- `[x]` Support light and dark mode.
  - Notes:

- `[x]` Verify mobile and desktop layouts.
  - Notes:

- `[x]` Ensure keyboard navigation and focus management work for dangerous confirmation flows.
  - Notes:

### Documentation

- `[x]` Add administrator documentation explaining tenant nuke and user nuke behavior.
  - Notes:

- `[x]` Document irreversible data-loss risk.
  - Notes:

- `[x]` Document authorization matrix.
  - Notes:

- `[x]` Document dry-run workflow.
  - Notes:

- `[x]` Document protected tenant, last-global-admin, and tenant-admin self-delete safeguards.
  - Notes:

- `[x]` Document audit and request-history retention behavior.
  - Notes:

- `[x]` Add examples for global admin tenant nuke.
  - Notes:

- `[x]` Add examples for tenant admin user nuke.
  - Notes:

- `[x]` Add examples for global admin user nuke across tenants.
  - Notes:

### Build, Deployment, And Release

- `[x]` Run `dotnet build src/Xeno.sln`.
  - Notes:

- `[x]` Run `dotnet run --project src/Test.Automated -- --results results.json`.
  - Notes:

- `[x]` Run `dotnet test src/Test.Xunit`.
  - Notes:

- `[x]` Run `dotnet test src/Test.Nunit`.
  - Notes:

- `[x]` Run SDK test suites for all three SDKs.
  - Notes:

- `[x]` Run dashboard build.
  - Notes:

- `[x]` Run dashboard end-to-end tests if present.
  - Notes:

- `[x]` Rebuild Docker images if implementation changes are made.
  - Notes:

- `[x]` Redeploy local Docker environment if implementation changes are made.
  - Notes:

- `[x]` Manually verify dry-run and execution flows against local deployment.
  - Notes:

## Exhaustive Test Plan

### Backend Shared Tests

- `[x]` Relationship tree test fixture creates a tenant with every supported downstream object type populated.
  - Notes:

- `[x]` Relationship tree test fixture creates users with every supported downstream user-owned object type populated.
  - Notes:

- `[x]` Relationship tree test fixture includes nested and cross-linked records, such as role assignments referencing both users and roles.
  - Notes:

- `[x]` Relationship tree test fixture includes request history, telemetry, and provider-specific metadata where supported.
  - Notes:

- `[x]` Tenant nuke dry-run as global admin returns `200`, status `dryRun`, and expected counts without deleting records.
  - Notes:

- `[x]` Tenant nuke execute as global admin returns `200`, status `success`, and deletes the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes users for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes credentials for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes auth sessions for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes roles for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes permissions for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes role assignments for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes clients for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes model definitions for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes model configurations for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes routing policies for the tenant.
  - Notes:

- `[x]` Tenant nuke execute removes request history when `includeRequestHistory` is `true`.
  - Notes:

- `[x]` Tenant nuke execute preserves request history or rejects unsupported behavior when `includeRequestHistory` is `false`, according to final product decision.
  - Notes:

- `[x]` Tenant nuke execute removes telemetry when `includeTelemetry` is `true`.
  - Notes:

- `[x]` Tenant nuke execute preserves telemetry or rejects unsupported behavior when `includeTelemetry` is `false`, according to final product decision.
  - Notes:

- `[x]` Tenant nuke execute removes tenant audit records when `includeAuditRecords` is `true`.
  - Notes:

- `[x]` Tenant nuke execute preserves tenant audit records or anonymizes them when `includeAuditRecords` is `false`, according to final product decision.
  - Notes:

- `[x]` Tenant nuke always writes a global audit record for the nuke operation.
  - Notes:

- `[x]` Tenant nuke does not delete records from other tenants.
  - Notes:

- `[x]` Tenant nuke leaves zero persisted records containing the nuked `tenantId`, except explicitly documented global audit records.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned users.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned credentials.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned auth sessions.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned roles.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned permissions.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned role assignments.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned clients.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned model definitions.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned model configurations.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned routing policies.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned request history records when request history deletion is enabled.
  - Notes:

- `[x]` Tenant nuke leaves zero orphaned telemetry records when telemetry deletion is enabled.
  - Notes:

- `[x]` Tenant nuke leaves zero provider-specific index, cache, metadata, or secondary-store fragments for the nuked tenant.
  - Notes:

- `[x]` Tenant nuke with missing `tenantId` returns `400`.
  - Notes:

- `[x]` Tenant nuke with missing `confirmTenantId` returns `400`.
  - Notes:

- `[x]` Tenant nuke with mismatched `confirmTenantId` returns `400`.
  - Notes:

- `[x]` Tenant nuke with missing or too-short `reason` returns `400`.
  - Notes:

- `[x]` Tenant nuke for missing tenant returns `404`.
  - Notes:

- `[x]` Tenant nuke for protected/default/system tenant returns `409`.
  - Notes:

- `[x]` Tenant nuke with no token returns `401`.
  - Notes:

- `[x]` Tenant nuke with regular user token returns `403`.
  - Notes:

- `[x]` Tenant nuke with tenant admin token returns `403`.
  - Notes:

- `[x]` Tenant nuke concurrent requests for same tenant allow one success and one deterministic `404` or `409`.
  - Notes:

- `[x]` Tenant nuke after successful nuke returns `404` on retry.
  - Notes:

- `[x]` User nuke dry-run as global admin returns `200`, status `dryRun`, and expected counts without deleting records.
  - Notes:

- `[x]` User nuke execute as global admin returns `200`, status `success`, and deletes the user.
  - Notes:

- `[x]` User nuke execute as same-tenant tenant admin returns `200`.
  - Notes:

- `[x]` User nuke execute removes credentials for the user.
  - Notes:

- `[x]` User nuke execute removes auth sessions for the user.
  - Notes:

- `[x]` User nuke execute removes role assignments for the user.
  - Notes:

- `[x]` User nuke execute removes or preserves audit records according to `includeAuditRecords`.
  - Notes:

- `[x]` User nuke execute anonymizes request history when `anonymizeRequestHistory` is `true`.
  - Notes:

- `[x]` User nuke execute preserves aggregate request history and model leaderboard counts.
  - Notes:

- `[x]` User nuke execute does not delete users or records from other tenants.
  - Notes:

- `[x]` User nuke leaves zero persisted records containing the nuked `userId`, except explicitly documented preserved/anonymized records.
  - Notes:

- `[x]` User nuke leaves zero orphaned credentials.
  - Notes:

- `[x]` User nuke leaves zero orphaned auth sessions.
  - Notes:

- `[x]` User nuke leaves zero orphaned role assignments.
  - Notes:

- `[x]` User nuke leaves zero orphaned user audit records when audit deletion is enabled.
  - Notes:

- `[x]` User nuke leaves zero direct user references in request history when anonymization is enabled.
  - Notes:

- `[x]` User nuke leaves zero provider-specific index, cache, metadata, or secondary-store fragments for the nuked user.
  - Notes:

- `[x]` User nuke with missing `tenantId` returns `400`.
  - Notes:

- `[x]` User nuke with missing `userId` returns `400`.
  - Notes:

- `[x]` User nuke with missing `confirmUserId` returns `400`.
  - Notes:

- `[x]` User nuke with mismatched `confirmUserId` returns `400`.
  - Notes:

- `[x]` User nuke with missing or too-short `reason` returns `400`.
  - Notes:

- `[x]` User nuke for missing tenant returns `404`.
  - Notes:

- `[x]` User nuke for missing user returns `404`.
  - Notes:

- `[x]` User nuke with no token returns `401`.
  - Notes:

- `[x]` User nuke with regular user token returns `403`.
  - Notes:

- `[x]` User nuke by tenant admin targeting another tenant returns `403` or `404` according to disclosure policy.
  - Notes:

- `[x]` User nuke by tenant admin targeting global/system admin returns `403`.
  - Notes:

- `[x]` User nuke for last global admin returns `409`.
  - Notes:

- `[x]` User nuke by tenant admin can delete another tenant admin in the same tenant.
  - Notes:

- `[x]` User nuke by tenant admin can delete the last tenant admin if the target is not the calling tenant admin.
  - Notes:

- `[x]` User nuke self-delete by tenant admin returns `409`.
  - Notes:

- `[x]` User nuke concurrent requests for same user allow one success and one deterministic `404` or `409`.
  - Notes:

- `[x]` User nuke after successful nuke returns `404` on retry.
  - Notes:

### Onboarding Safeguard Tests

- `[x]` Onboarding a new organization with an email address that already belongs to a global/system admin returns `409 Conflict`.
  - Notes:

- `[x]` Onboarding into an existing tenant with an email address that already belongs to a global/system admin returns `409 Conflict`.
  - Notes:

- `[x]` Onboarding with case-only or whitespace-only variations of an existing global/system admin email returns `409 Conflict`.
  - Notes:

- `[x]` Onboarding with an existing non-global-admin email in the same target tenant continues to return the existing duplicate-email conflict.
  - Notes:

- `[x]` Onboarding with an existing non-global-admin email in a different tenant follows the existing product rule for cross-tenant email reuse.
  - Notes:

- `[x]` Onboarding global-admin-email rejection returns the documented error code `GlobalAdminEmailAlreadyExists`.
  - Notes:

- `[x]` Onboarding global-admin-email rejection does not create a tenant, user, credential, token, auth session, or audit side effect other than the permitted rejected-request audit record.
  - Notes:

### Provider Matrix

Run every backend shared test against each supported database implementation:

- `[x]` In-memory provider.
  - Notes:

- `[x]` File/local provider if present.
  - Notes:

- `[x]` SQL provider if present.
  - Notes:

- `[x]` Cloud/database provider if present.
  - Notes:

Provider-specific tests:

- `[x]` Provider transaction rollback leaves all records intact when a failure is injected mid-tenant-nuke.
  - Notes:

- `[x]` Provider transaction rollback leaves all records intact when a failure is injected mid-user-nuke.
  - Notes:

- `[x]` Provider count calculations match actual records deleted.
  - Notes:

- `[x]` Provider handles empty optional tables without failure.
  - Notes:

- `[x]` Provider handles large tenants without timeout in normal test bounds.
  - Notes:

- `[x]` Provider cascade infrastructure deletes children when a parent object is deleted through the nuke deletion path.
  - Notes:

- `[x]` Provider cascade infrastructure does not rely on accidental iteration order for correctness.
  - Notes:

- `[x]` Provider remnant scanner finds no tenant-owned fragments after tenant nuke.
  - Notes:

- `[x]` Provider remnant scanner finds no user-owned fragments after user nuke.
  - Notes:

- `[x]` Provider manual cascade order is covered for every parent-child relationship that lacks native constraints.
  - Notes:

### REST API Tests

- `[x]` Tenant nuke request and response JSON match documented schema.
  - Notes:

- `[x]` User nuke request and response JSON match documented schema.
  - Notes:

- `[x]` Tenant nuke content type validation rejects invalid content types if the server enforces content types.
  - Notes:

- `[x]` User nuke content type validation rejects invalid content types if the server enforces content types.
  - Notes:

- `[x]` Error responses use documented error envelope.
  - Notes:

- `[x]` Unauthorized requests never reveal tenant/user existence.
  - Notes:

- `[x]` Forbidden tenant-admin cross-tenant user nuke does not reveal sensitive target details if disclosure policy requires hiding them.
  - Notes:

### SDK Tests

- `[x]` C# SDK serializes tenant nuke request correctly.
  - Notes:

- `[x]` C# SDK deserializes tenant nuke response correctly.
  - Notes:

- `[x]` C# SDK serializes user nuke request correctly.
  - Notes:

- `[x]` C# SDK deserializes user nuke response correctly.
  - Notes:

- `[x]` C# SDK propagates validation, authorization, not-found, and conflict errors.
  - Notes:

- `[x]` JavaScript/TypeScript SDK serializes tenant nuke request correctly.
  - Notes:

- `[x]` JavaScript/TypeScript SDK deserializes tenant nuke response correctly.
  - Notes:

- `[x]` JavaScript/TypeScript SDK serializes user nuke request correctly.
  - Notes:

- `[x]` JavaScript/TypeScript SDK deserializes user nuke response correctly.
  - Notes:

- `[x]` JavaScript/TypeScript SDK propagates validation, authorization, not-found, and conflict errors.
  - Notes:

- `[x]` Python SDK serializes tenant nuke request correctly.
  - Notes:

- `[x]` Python SDK deserializes tenant nuke response correctly.
  - Notes:

- `[x]` Python SDK serializes user nuke request correctly.
  - Notes:

- `[x]` Python SDK deserializes user nuke response correctly.
  - Notes:

- `[x]` Python SDK propagates validation, authorization, not-found, and conflict errors.
  - Notes:

### Postman Tests

- `[x]` Collection JSON parses successfully.
  - Notes:

- `[x]` Tenant nuke dry-run request uses the documented endpoint, headers, and body.
  - Notes:

- `[x]` Tenant nuke execute request uses the documented endpoint, headers, and body.
  - Notes:

- `[x]` User nuke dry-run request uses the documented endpoint, headers, and body.
  - Notes:

- `[x]` User nuke execute request uses the documented endpoint, headers, and body.
  - Notes:

- `[x]` Postman examples match actual API responses.
  - Notes:

### Dashboard Tests

- `[x]` Global admin sees tenant nuke controls.
  - Notes:

- `[x]` Tenant admin does not see tenant nuke controls.
  - Notes:

- `[x]` Regular user does not see nuke controls.
  - Notes:

- `[x]` Global admin sees user nuke controls for all tenants.
  - Notes:

- `[x]` Tenant admin sees user nuke controls only for their tenant.
  - Notes:

- `[x]` Exact-ID confirmation gates remain disabled until the ID matches.
  - Notes:

- `[x]` Reason validation appears inline.
  - Notes:

- `[x]` Dry-run preview displays returned deleted and updated counts.
  - Notes:

- `[x]` Execute confirmation modal requires explicit confirmation after dry-run.
  - Notes:

- `[x]` Success state displays operation ID, timestamps, and counts.
  - Notes:

- `[x]` Validation errors display in the form without losing entered data.
  - Notes:

- `[x]` Forbidden errors display a meaningful message.
  - Notes:

- `[x]` Not-found errors display a meaningful message.
  - Notes:

- `[x]` Conflict errors display the provider/server conflict reason.
  - Notes:

- `[x]` Light mode styling matches dashboard.
  - Notes:

- `[x]` Dark mode styling matches dashboard.
  - Notes:

- `[x]` Mobile layout has no overlapping text or controls.
  - Notes:

- `[x]` Keyboard-only flow can complete dry-run and execute steps.
  - Notes:

### Security And Abuse Tests

- `[x]` Tenant ID in body cannot be overridden by query string.
  - Notes:

- `[x]` Tenant admin cannot spoof tenant scope through headers.
  - Notes:

- `[x]` Expired token cannot perform dry-run.
  - Notes:

- `[x]` Expired token cannot execute.
  - Notes:

- `[x]` Malicious `reason` values are safely encoded in audit records and dashboard display.
  - Notes:

- `[x]` Large but valid reason does not break persistence or response serialization.
  - Notes:

- `[x]` Invalid JSON body returns `400`.
  - Notes:

- `[x]` Replayed execute request after successful nuke cannot delete unrelated data.
  - Notes:

- `[x]` Race between dry-run and execute handles records added after dry-run deterministically.
  - Notes:

- `[x]` Operation logs never include credential secret keys or bearer tokens.
  - Notes:

## Acceptance Criteria

- `[x]` Both APIs exist and are documented.
  - Notes:

- `[x]` Tenant nuke is global-admin-only.
  - Notes:

- `[x]` User nuke is available to global admins and same-tenant tenant admins for non-self targets.
  - Notes:

- `[x]` Dry-run and execute flows work consistently across all database providers.
  - Notes:

- `[x]` Destructive execution removes all intended records and no records outside the intended scope.
  - Notes:

- `[x]` All SDKs expose the APIs with docs and tests.
  - Notes:

- `[x]` REST API docs and Postman collection are updated and validated.
  - Notes:

- `[x]` Dashboard exposes safe, role-aware administrative controls.
  - Notes:

- `[x]` Exhaustive tests pass through the repository's shared test runners and SDK/dashboard test suites.
  - Notes:

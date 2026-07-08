# Cascade Relationship Tree

This document records the parent-child relationships enforced by administrative nuke operations.

## Tenant Nuke

| Parent | Child | Ownership Field | Behavior |
| --- | --- | --- | --- |
| Tenant | Tenant | `Tenant.Id` | Delete the tenant record. |
| Tenant | User | `User.TenantId` | Delete. |
| Tenant | Credential | `Credential.TenantId` | Delete. |
| Tenant | AuthSession | `AuthSession.TenantId` | Delete. |
| Tenant | Role | `Role.TenantId` | Delete. |
| Tenant | Permission | `Permission.TenantId` | Delete. |
| Tenant | RoleAssignment | `RoleAssignment.TenantId` | Delete. |
| Tenant | AuditRecord | `AuditRecord.TenantId` or `AuditRecord.TargetTenantId` | Delete when `includeAuditRecords` is true. The nuke operation audit record is written after deletion under the caller tenant. |
| Tenant | XenoClientRecord | `XenoClientRecord.TenantId` | Delete and drop active in-memory client connection. |
| Tenant | ModelDefinition | `ModelDefinition.TenantId` | Delete. |
| Tenant | ModelConfiguration | `ModelConfiguration.TenantId` | Delete. |
| Tenant | RoutingPolicy | `RoutingPolicy.TenantId` | Delete. |
| Tenant | RequestHistoryEntry | `RequestHistoryEntry.TenantId` or `RequestHistoryEntry.RequestorTenantId` | Delete when `includeRequestHistory` is true. |
| Tenant | Client telemetry samples | `tenant_id` | Delete when `includeTelemetry` is true through provider purge methods. |
| Tenant | Provider telemetry samples, metrics, and models | `tenant_id` | Delete when `includeTelemetry` is true through provider purge methods. |
| Tenant | Telemetry collection attempts | `tenant_id` | Delete when `includeTelemetry` is true through provider purge methods. |
| Tenant | Provider-specific indexes/cache/metadata | Provider-specific tenant key | Delete through provider purge methods when present. |

## User Nuke

| Parent | Child | Ownership Field | Behavior |
| --- | --- | --- | --- |
| User | User | `User.Id` and `User.TenantId` | Delete. |
| User | Credential | `Credential.UserId` and `Credential.TenantId` | Delete. |
| User | AuthSession | `AuthSession.UserId` or deleted credential ID | Delete. |
| User | RoleAssignment | `PrincipalType=User` and `PrincipalId=User.Id` | Delete. |
| Credential owned by user | RoleAssignment | `PrincipalType=Credential` and `PrincipalId=Credential.Id` | Delete. |
| User | AuditRecord | `AuditRecord.PrincipalId` or `AuditRecord.TargetResourceId` | Delete when `includeAuditRecords` is true. Otherwise preserved as audit evidence. |
| User | RequestHistoryEntry | `UserId`, `RequestorPrincipalId`, `CredentialId`, or `SessionId` | Anonymize by default when `anonymizeRequestHistory` is true. |
| User | Provider-specific indexes/cache/metadata | Provider-specific user key | No current provider-specific user store exists; add provider cleanup if one is introduced. |

## Validation Rules

- Tenant nuke execution with request-history deletion enabled must leave zero platform records containing the target tenant ID, except the post-operation audit record under the caller tenant.
- User nuke execution with request-history anonymization enabled must leave zero platform records containing the target user ID, except preserved audit records when audit deletion is disabled and the post-operation audit record.
- Existing provider telemetry cascade infrastructure deletes child telemetry rows before parent sample rows for database stores that persist telemetry outside the serialized platform state.
- Providers without native foreign-key cascades must delete children explicitly in leaf-to-root order.


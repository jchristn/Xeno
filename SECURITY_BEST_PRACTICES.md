# Xeno Security Best Practices

Xeno turns a set of machines into a shared accelerator fabric. That fabric may include cloud controllers, data-center servers, laptops, workstations, and home-lab GPUs. Security in Xeno therefore has two jobs at once: it protects the control plane, and it protects the machines that volunteer compute to that control plane.

Treat the Xeno server as a privileged coordinator. It receives API requests, authenticates callers, chooses clients, forwards work to connected machines, records request history, and may collect fleet telemetry. Treat each Xeno client as an agent with access to local provider endpoints and local machine telemetry. A compromised server can send work to clients. A compromised client can expose local model endpoints, local request history, and provider behavior. Good deployments reduce both risks by separating credentials, limiting permissions, binding every operation to a tenant, and keeping local dashboards private.

## Security Model At A Glance

Xeno uses tenant-scoped identity and authorization. Every protected request resolves to a tenant, authenticates a principal, and then checks whether that principal may perform the requested operation. The important split is between users, credentials, and sessions:

- Users represent people. Users may authenticate with username or email plus password through login endpoints. Passwords are stored as salted SHA-256 hashes.
- Credentials represent non-interactive access. A credential has an access key, belongs to a tenant, and is owned by a user.
- Sessions represent short-lived API access. Bearer tokens are issued from user login or credential exchange, stored server-side as token hashes, and expire according to `Auth.SessionMinutes`.

The REST API accepts bearer tokens through `Authorization: Bearer ...` or `x-token`. It also accepts direct credential authentication through `x-access-key`. The client WebSocket is stricter: Xeno clients must authenticate with `x-access-key` and a tenant header. A WebSocket client may not authenticate with a bearer token, user password, or `x-secret-key`.

Authorization is role based. Roles contain permissions, permissions name a resource type and operation, and role assignments bind users or credentials to roles. Xeno also has administrator bypasses for users marked `IsAdmin` or `IsTenantAdmin`. Explicit deny permissions override permits, and no matching permission means access is denied.

## Main Trust Boundaries

Xeno has several security boundaries that operators should keep separate in their mental model.

The server REST API is the public control-plane surface. It exposes OpenAI-compatible inference routes, management APIs, dashboard APIs, OpenAPI metadata, metrics, request history, telemetry, tenants, users, credentials, roles, permissions, and settings. In any shared or remote deployment, REST authentication must be enabled and TLS must terminate before traffic reaches the server.

The client WebSocket is the agent-control surface. Connected clients trust the server enough to receive routed inference requests and telemetry collection requests. Give every client its own credential where possible. Do not share one long-lived credential across a fleet unless the machines have the same owner, same permissions, and same rotation schedule.

The local client dashboard is a workstation-local inspection surface. It stores local request history in SQLite, including request and response headers, bounded bodies, timing, token metadata, and stream counters. It is designed to bind to `127.0.0.1` and require a local token. Do not expose it on an untrusted network.

The database is the persistence boundary. Xeno stores platform state, users, credentials, sessions, roles, audit records, request history, and telemetry in the configured database. Database access is equivalent to administrative access to the deployment. Use least-privilege database accounts, restrict network access, and back up the database as sensitive material.

## Authentication

Xeno supports several authentication flows because different actors need different credentials.

Dashboard and user login use `/v1.0/api/auth/dashboard/login` or `/v1.0/api/auth/login`. A user supplies tenant information plus username/email and password. On dashboard login, Xeno issues a bearer token for that user. On client login, Xeno issues a tenant-bound client access key so the client can store it in `~/.xeno/client.json`.

Credential token exchange uses `/v1.0/api/auth/token`. A caller submits an access key, and Xeno returns a bearer token. Bearer tokens carry the tenant and principal identity through a server-side session record; the raw token is not stored. The stored session contains a token hash, principal type, user or credential reference, tenant ID, expiration time, and active flag.

Direct credential authentication is accepted on REST requests by sending `x-access-key` and the tenant header. The SDKs can use either bearer tokens or direct credential headers. Direct credentials are convenient for automation, but bearer tokens are better for short-lived sessions and revocation workflows.

Client WebSocket authentication always uses direct credentials. A client connects to the configured WebSocket path, normally `/ws/v1/clients`, and supplies `x-access-key` and `x-tenant-guid`. The server rejects bearer-token and `x-secret-key` WebSocket authentication to keep user sessions and machine agents separate.

### Required Headers

Use these headers consistently:

| Header | Purpose |
| --- | --- |
| `Authorization: Bearer <token>` | Primary REST session token carrier. |
| `x-token` | Alternate REST session token carrier. If both bearer and `x-token` are present, they must match. |
| `x-access-key` | Public credential identifier. |
| `x-tenant-guid` | Tenant identifier. |
| `x-tenant-id` | Alternate tenant identifier. |

Tenant hints must not conflict. Xeno resolves tenants from `x-tenant-guid`, `x-tenant-id`, the `tenantId` query parameter, request bodies that carry a tenant ID, or the default tenant when nothing else is supplied. If multiple tenant hints are present and do not agree, the request is rejected.

## Authorization

Every protected route calls the authorization layer with a resource type and operation. Examples include `Users:Read`, `Credentials:Write`, `Clients:Delete`, `ClientTelemetry:Read`, `ClientTelemetry:Write`, `RoutingPolicies:Write`, `Inference:Execute`, `RequestHistory:Read`, and `Admin:Admin`.

Xeno grants access in this order:

1. Unauthenticated requests fail with `401`.
2. Missing or inactive tenants fail.
3. Missing or inactive users and credentials fail.
4. System administrators may cross tenant boundaries where the route allows it.
5. Tenant mismatches fail with `403`.
6. Tenant administrators may bypass RBAC within their own tenant.
7. Active role assignments are expanded into permissions.
8. Explicit denies win.
9. Matching permits grant access.
10. No match fails with `403`.

Users marked `IsAdmin` have system-wide administrative capability. Users marked `IsTenantAdmin` have full administrative capability inside their tenant. Those flags are powerful and should be rare. Prefer roles and permissions for ordinary users and credentials, because role assignments are easier to reason about and revoke.

Credentials do not receive user administrator bypasses. A credential authenticates as a credential principal and receives only the roles assigned to that credential principal. That behavior is useful: an administrator can own a credential that only has inference execution, telemetry read, or one operational role rather than inheriting every administrative permission of the owning user.

## Built-In And Seeded Credentials

Local development and factory assets may include seeded credentials such as the `default` access key, `password`, or `development-only-change-me`. They are development conveniences, not deployment secrets.

Before using Xeno outside a private local development loop:

- Change `Auth.SigningKey`.
- Remove or rotate all seeded credentials.
- Change the default admin password.
- Create named credentials for clients and applications.
- Disable or delete credentials that are no longer needed.
- Keep `Rest.RequireAuthentication` enabled.

The Docker deployment under `docker/xeno.json` currently enables REST authentication. The factory asset under `docker/factory/xeno.json` is intentionally more permissive and has `Rest.RequireAuthentication` set to `false`; do not treat factory settings as production settings.

## Passwords, Secrets, And Tokens

User passwords are salted and hashed. Credential records retain a legacy secret field for compatibility with existing state files, but credential authentication does not depend on that field and clients must not send it.

Bearer tokens are generated from cryptographically random bytes and stored only as hashes in auth session records. Sessions have an expiration time, an active flag, and a principal reference. Logout revokes the current session. Token refresh issues a new token and revokes the prior session when the prior session is known.

Recommended handling:

- Store server signing keys, client access keys, and provider API keys in a secrets manager or protected host configuration.
- Avoid committing `client.json`, `xeno.json`, `.env`, SQLite databases, or backups that contain real credentials.
- Use separate credentials for dashboard users, API integrations, clients, and automation.
- Rotate credentials after staff changes, machine retirement, accidental exposure, or suspicious logs.
- Keep token lifetimes short enough for your operating model. The default session lifetime is 60 minutes.

## REST API And Dashboard Security

`Rest.RequireAuthentication` controls whether management and inference routes require authentication. Health, Swagger, OpenAPI metadata, and metrics are special surfaces; review whether those endpoints should be exposed in your environment.

The dashboard uses the REST API. In a browser deployment, CORS settings determine which origins may call the server. Keep `Cors.AllowedOrigins` tight. The default Docker configuration allows `http://localhost:9100`, which is suitable for the local dashboard container. Do not use broad wildcard origins for authenticated dashboard deployments.

Xeno exposes OpenAI-compatible routes through the same authorization layer as management APIs. A credential needs `Inference:Execute` to run model requests. A credential that can list clients does not automatically get inference execution, and an inference credential should not automatically get access to users, credentials, settings, backup, restore, request history, or telemetry.

Use bearer tokens for browser dashboards and direct credentials for machine clients or server-side integrations. Avoid placing long-lived direct credential secrets in browser code.

## Client WebSocket Security

The WebSocket connection is how clients receive routed work and telemetry collection requests. Every client connection authenticates as a credential principal. After registration, the client advertises its provider endpoints and model inventory.

Recommended client credential practices:

- Issue one credential per machine or per small operational group.
- Name credentials so operators can identify the host or automation that owns them.
- Deactivate a credential before decommissioning a client.
- Restrict client credentials to the roles they need. A client credential usually needs client registration and inference participation, not tenant administration.
- Rotate client credentials periodically and whenever a machine is rebuilt or reassigned.

The server forwards a controlled set of headers to clients, including requestor IP, Xeno request ID, and tenant ID. The local dashboard redacts sensitive headers before storing them. Provider credentials configured in the client are used only for local upstream calls and are redacted from dashboard provider responses.

## Local Dashboard Security

The local dashboard is useful during development and operations because it shows the exact requests processed by one client. It also stores data that may be sensitive. By default, the local dashboard listens on `127.0.0.1`, uses a local token, and stores SQLite history at `~/.xeno/local-dashboard.db`.

Keep the local dashboard private:

- Leave `LocalDashboard.Host` set to `127.0.0.1` unless you have a deliberate reason to expose it.
- Leave `LocalDashboard.RequireLocalToken` enabled.
- Do not share dashboard URLs or local tokens.
- Treat `~/.xeno/local-dashboard.db` as sensitive. It may contain prompts, responses, headers, timing, model names, and provider behavior.
- Reduce `HistoryRetention.MaxEntries`, lower `HistoryRetention.MaxBodyBytes`, or disable `HistoryRetention.PersistBodies` on machines that process sensitive workloads.
- Delete local request history before transferring or repurposing a machine.

Binding the local dashboard to `0.0.0.0` changes the risk profile. Anyone who can reach that port may reach a surface designed for local operators. If remote access is required, put it behind a secure tunnel, VPN, or authenticated reverse proxy instead of exposing the port directly.

## Request History And Data Capture

Xeno records server request history and local client request history. Server request history can include selected client, model, status, duration, route decision, resolved requestor tenant ID, resolved requestor principal type and ID, request headers, response headers, bounded request body, bounded response body, timing, token metadata, and stream counters. Local request history records similar details from the client’s point of view when the server dispatches those fields to the client.

Headers are redacted for common sensitive names such as authorization, tokens, cookies, secrets, credentials, and API keys. Redaction is not a substitute for data minimization. Prompts and responses can carry secrets even when headers do not.

Server email settings can contain provider secrets such as `Email.Mailgun.ApiKey`. Treat them the same way as database passwords and signing keys: keep them out of committed production settings, rotate them after exposure, and avoid copying them into screenshots or issue reports. Xeno notification templates are intentionally non-secret; they must not include user passwords, bearer tokens, access keys, secret keys, or hashed credential material. Use the file email provider for local inspection rather than sending test messages to real recipients.

For sensitive environments:

- Disable body persistence where inspection is not required.
- Keep retention short.
- Limit access to `RequestHistory:Read`.
- Treat request-history exports and backups as sensitive data.
- Verify that upstream applications do not send secrets in prompts, query strings, or custom headers.
- Use separate tenants for environments that should not share request-history visibility.

## Telemetry Collection

Server-collected client telemetry is enabled by default in the current settings. The server periodically asks connected clients for host telemetry and provider telemetry, then persists normalized samples.

Host telemetry includes CPU, memory, disk, and GPU utilization where available. Provider telemetry can include provider health, model counts, loaded model counts, active request count, queued request count, latency, token rates, cache pressure, process correlation, and bounded provider metrics. Telemetry is operational data, but it can still reveal capacity, topology, model inventory, endpoint labels, and workload patterns.

Important controls:

| Setting | Purpose |
| --- | --- |
| `TelemetryCollection.Enabled` | Turns server-requested telemetry collection on or off. |
| `CollectHostTelemetry` | Controls CPU, memory, disk, and GPU host metrics. |
| `CollectProviderTelemetry` | Controls provider health and model metrics. |
| `PersistRawSnapshots` | Stores bounded raw snapshots in addition to normalized rows. |
| `MaxRawSnapshotBytes` | Caps raw snapshot JSON size. |
| `RetentionDays` | Controls telemetry retention. |
| `MaxConcurrentClientRequests` | Limits collection fan-out per cycle. |

Disable raw snapshot persistence if normalized telemetry is enough. Keep retention proportional to operational needs. Restrict `ClientTelemetry:Read`, `ClientTelemetry:Write`, and `ClientTelemetry:Delete` permissions to operators who need them.

## Routing, Forwarded Headers, And Client Privacy

When Xeno routes an inference request, the server adds forwarding headers such as:

- `X-Forwarded-For`
- `X-Real-IP`
- `X-Xeno-Requestor-IP`
- `X-Xeno-Request-ID`
- `X-Xeno-Tenant-ID`

Those headers help clients correlate requests and preserve caller context. They also reveal tenant and request identity to the selected client. Only connect clients operated by people or systems allowed to see that routing metadata.

The server also returns routing trace headers to callers, including selected client information, routing policy details, timing, candidate counts, and token metrics where available. That visibility is useful for debugging, but it can disclose topology. Avoid exposing detailed routing headers to untrusted external callers unless they are meant to understand the fleet.

## Multi-Tenant Operation

Tenant isolation is enforced by tenant resolution and tenant-scoped queries. A user, credential, role, permission, assignment, client, request-history row, and telemetry row belongs to a tenant. The authorization layer rejects tenant mismatches unless a system administrator is performing an operation that explicitly allows cross-tenant access.

A few rules keep multi-tenant deployments sane:

- Use one tenant per organization, department, partner, or isolation boundary.
- Do not reuse credentials across tenants.
- Keep tenant IDs out of user-controlled application logic where possible; server-side integration code should set them explicitly.
- Give tenant administrators power only inside their tenant.
- Audit role and permission changes after onboarding or incident response.
- Test tenant-bound credentials by attempting reads against a different tenant and confirming they fail.

## RBAC Design Guidance

Start with small roles. Large catch-all roles are easier to create and harder to audit.

Useful production roles often look like this:

- `InferenceRunner`: `Inference:Execute` only.
- `TelemetryViewer`: `ClientTelemetry:Read` only.
- `TelemetryOperator`: `ClientTelemetry:Read` and `ClientTelemetry:Write`.
- `ClientOperator`: read clients, delete stale clients, view advertised models.
- `RoutingOperator`: read and write routing policies.
- `Auditor`: read request history, audit records, and effective permissions.
- `SecurityAdmin`: manage users, credentials, roles, permissions, and assignments.

Credentials should receive narrower roles than people. A CI job that exercises inference does not need to create users. A dashboard user who reads telemetry does not need to rotate client credentials. A client machine does not need to edit routing policies.

Use deny permissions sparingly. They are valuable for guardrails, but too many explicit denies make access hard to reason about. Prefer not assigning a role in the first place unless a denial is needed to override a broader inherited grant.

## Audit, Metrics, And Investigation

Xeno appends audit records for failed authorization checks and for privileged administrator bypasses. Login failures and dashboard login failures are audited. The server also tracks authentication success and failure counters, authorization denial counters, and privileged bypass counts for metrics.

Useful investigation workflow:

1. Start with the request ID.
2. Check request history for path, model, selected client, status, headers, and routing decision.
3. Check audit records for authorization denials or administrator bypasses near the same time.
4. Check client local dashboard history if the request reached a client.
5. Check server and client logs for connection, WebSocket, provider, and telemetry errors.

Keep audit data long enough to answer operational questions, but remember that audit and request history can include sensitive operational metadata. Restrict access to both.

## Deployment Configuration

Security starts with the configuration file. The server settings control REST exposure, authentication, CORS, WebSocket behavior, database connection, auth token lifetime, and telemetry collection.

Recommended non-local baseline:

```json
{
  "Rest": {
    "Hostname": "0.0.0.0",
    "Port": 9000,
    "Ssl": false,
    "RequireAuthentication": true
  },
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": ["https://dashboard.example.com"],
    "AllowedHeaders": ["Authorization", "Content-Type", "x-token", "x-tenant-guid", "x-tenant-id"],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
  },
  "Auth": {
    "Issuer": "xeno",
    "SigningKey": "replace-with-a-long-random-secret",
    "SessionMinutes": 60,
    "MaxLoginRequestsPerMinute": 20
  }
}
```

If TLS is terminated by a reverse proxy or ingress controller, leave `Rest.Ssl` disabled in Xeno and enforce HTTPS at the proxy. If Xeno itself is exposed directly, use TLS at the network edge and block direct plain HTTP access.

For Docker deployments:

- Keep the server port reachable only from intended callers.
- Keep the dashboard origin aligned with `Cors.AllowedOrigins`.
- Mount configuration and database volumes with host permissions that exclude ordinary users.
- Do not bake real secrets into images.
- Treat SQLite files and database dumps as sensitive backups.
- Prefer a managed database with restricted credentials for shared deployments.

## Network And Infrastructure Controls

Xeno’s application controls should be paired with network controls. Put the server behind a reverse proxy, load balancer, VPN, or private network boundary appropriate for the deployment. Restrict database access to the server. Restrict client egress to the server and provider endpoints it needs.

For a cloud controller with volunteer or partner GPUs, assume clients are not equally trusted. Use separate tenants or separate credentials for different groups. Do not route confidential prompts to clients owned by people who should not see them. If clients are outside your administrative boundary, document that request payloads and routing metadata are visible to the selected client machine.

For a data center deployment, the controller can usually live on a private management network. Clients should connect outbound to the controller. Inbound access to client machines is not required for the Xeno WebSocket pattern, which makes firewall rules simpler.

## Data Classification

Classify Xeno data before onboarding real workloads:

| Data | Sensitivity |
| --- | --- |
| User passwords | Secret; stored as salted hashes. |
| Credential access keys | Sensitive; usable for direct credential authentication. |
| Bearer tokens | Secret; short-lived but immediately usable. |
| Client provider credentials | Secret; stored in client configuration when configured. |
| Request bodies and response bodies | Potentially sensitive or regulated content. |
| Request headers | Potentially sensitive; redacted for known secret names. |
| Request history | Sensitive operational record. |
| Telemetry | Operationally sensitive; reveals capacity, topology, and workload patterns. |
| Audit records | Sensitive security record. |
| Database backups | Sensitive aggregate of all of the above. |

The safest habit is to treat every Xeno database and client-local dashboard database as confidential, even when the current workload is synthetic.

## Operating Checklist

Before a real deployment:

- Enable `Rest.RequireAuthentication`.
- Replace `Auth.SigningKey`.
- Rotate default users, passwords, and credentials.
- Use HTTPS at the edge.
- Restrict CORS to the actual dashboard origin.
- Create least-privilege roles for users, credentials, clients, telemetry, and inference.
- Give each client or client group a distinct credential.
- Keep the local dashboard bound to `127.0.0.1`.
- Decide whether request bodies should be stored.
- Decide whether raw telemetry snapshots should be stored.
- Protect database files, volumes, connection strings, and backups.
- Confirm tenant mismatch attempts fail.
- Confirm inference credentials cannot manage users or credentials.
- Confirm dashboard users cannot read other tenants.

Daily or weekly operations:

- Review failed login and authorization audit records.
- Review privileged bypass events.
- Remove stale clients and inactive credentials.
- Rotate secrets on schedule.
- Check telemetry retention and database growth.
- Verify backups can be restored and are stored securely.

After an incident:

- Revoke exposed sessions.
- Rotate affected credentials and provider keys.
- Disable suspicious users or credentials.
- Export relevant audit and request-history records for investigation.
- Reduce retention or disable body capture if payload exposure was broader than expected.
- Rebuild affected clients if local configuration or provider credentials may have been copied.

## Known Security Tradeoffs

Xeno is still intentionally practical for local development. That means some defaults and compatibility paths are not production posture:

- Factory Docker configuration may disable REST authentication.
- Development signing keys and seeded credentials exist for bootstrap.
- Legacy credential secret fields may exist in older state files, but they are ignored for authentication.
- Request history can persist prompts, responses, and headers.
- Local dashboard databases live on the client machine.
- Telemetry can reveal topology and model inventory.

None of these are unusual for an early control-plane deployment, but they need explicit operational decisions. A secure Xeno installation is not just a running server with a password changed. It is a set of tenants, roles, credentials, retention settings, network rules, and dashboard settings chosen for the data and machines being federated.

## Practical Defaults For Different Environments

For a single developer laptop, use local-only listeners, keep the local dashboard token enabled, and avoid putting real provider secrets into committed files. Request history is useful here; keep body capture on if you need debugging and clear it when done.

For a team lab, enable REST authentication, create separate users, issue one credential per client, and restrict CORS to the team dashboard. Keep telemetry enabled, but review whether raw snapshots are needed. Use roles rather than shared admin credentials.

For a data center, treat Xeno as infrastructure. Put the server on a management network, use a proper database, protect backups, and monitor audit records. Clients should be provisioned with host-specific credentials and retired credentials should be disabled during decommissioning.

For a community GPU network, do not assume every client operator should see every workload. Use tenants to separate groups, make routing policies explicit, disclose what request data reaches selected clients, and keep sensitive workloads on trusted machines only.

Good Xeno security comes from preserving the boundaries the platform already gives you: tenants for isolation, users for people, credentials for machines and integrations, sessions for short-lived API access, RBAC for least privilege, local dashboards for local inspection, and retention controls for data minimization. Keep those boundaries clean and the platform stays understandable under pressure.

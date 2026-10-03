# Xeno Python SDK

The Xeno Python SDK is a standard-library HTTP client for Xeno v0.1.1 alpha.

## Install

Use it directly from this repository while Xeno is in alpha:

```bash
python -m pip install -e src/sdk/python
```

## Quick Start

```python
from xeno_client import XenoClient

client = XenoClient("http://localhost:9000")
health = client.get_health()
ping = client.ping(sequence=1)
models = client.list_openai_models()
policies = client.list_routing_policies()
leaderboard = client.get_model_token_leaderboard({"limit": 10, "offset": 0})
```

Onboarding is available through the unauthenticated signup route. Omit
`tenantId` to create a tenant and first tenant administrator; include `tenantId`
to add a regular user to an existing tenant. Validation failures return `400`;
duplicate email or username in the target tenant returns `409`. Requests using
an email address already held by a global administrator also return `409` with
`GlobalAdminEmailAlreadyExists`.

```python
public_client = XenoClient("http://localhost:9000", bearer_token=None)
onboarding = public_client.onboard({
    "tenantName": "Example Company",
    "email": "owner@example.com",
    "username": "owner@example.com",
    "password": "password",
})
```

Administrative nuke APIs are exposed through `nuke_tenant` and `nuke_user`.
Dashboard flows execute after exact-ID entry, an administrative reason, and a
confirmation modal. API callers can still send `dryRun: True` to preview
affected counts, but direct execution uses `dryRun: False`. Tenant nuke requires
a global administrator. User nuke requires a global administrator or a
same-tenant tenant administrator; tenant administrators cannot nuke themselves.

```python
result = client.nuke_user({
    "tenantId": "ten_customer",
    "userId": "usr_departed",
    "confirmUserId": "usr_departed",
    "dryRun": False,
    "reason": "User left the customer organization.",
})
```

The model token leaderboard is global when `tenantId` is omitted. Include
`tenantId` in the filter dictionary or POST body to scope results to one tenant.
`limit` and `offset` are optional integers; omit them or send `None`/JSON
`null` to use defaults. Each returned model usage object includes
`requestCount`, `totalTokens`, `promptTokens`, and `outputTokens`.

The SDK defaults to bearer token `default`, which works against the default local Docker server. Bearer credentials infer tenant context, so the SDK does not send `x-tenant-guid` unless `tenant_id` is explicitly set. Production deployments should pass an issued bearer token or a globally unique tenant credential value as the bearer token:

```python
client = XenoClient("http://localhost:9000", bearer_token=token)
```

Global administrators can send a provider-backed test email through the same standard Xeno HTML layout used by notification templates:

```python
response = client.send_test_email({
    "toEmail": "joel.christner@gmail.com",
    "title": "Xeno Test Email",
    "subject": "Xeno test email",
    "body": "hello world",
})
```

Legacy integrations can still send tenant-bound access credentials through `x-access-key`, but new REST integrations should prefer bearer authentication:

```python
client = XenoClient(
    "http://localhost:9000",
    bearer_token=None,
    tenant_id="ten_default",
    access_key=access_key,
)
```

Use least-privilege credentials with `Inference.Read` and `Inference.Execute`
for application traffic. Credential values may be sent as bearer tokens.
Tenant headers are optional selectors or compatibility hints; they do not
authenticate the request.

`get_settings()` and `update_settings()` return the server settings object itself.

## Interactive Console

```bash
python -m xeno_client.console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK covers authentication, onboarding, administrative nuke APIs, tenants, users, credentials, roles, permissions, grantable permissions, role assignments, clients, advertised models, per-client cross-tenant sharing (`set_client_sharing`), routing policies, OpenAI-compatible inference, Ollama-compatible inference, server-collected client telemetry, request history, public model token leaderboard, analytics, settings including email provider configuration and administrative test email sends, backup, health, ping, metrics, and OpenAPI.

The SDK targets the Xeno server API. The `xeno` client local dashboard API,
including local request history, machine telemetry, and provider runtime
telemetry routes, runs on the client machine, uses a per-process local token,
and is documented in
[../../../CLIENT_GETTING_STARTED.md](../../../CLIENT_GETTING_STARTED.md) rather
than wrapped by the server SDKs.

## Alpha Notice

Xeno v0.1.1 is alpha software. API shapes may change before a stable release.

# Xeno Python SDK

The Xeno Python SDK is a standard-library HTTP client for Xeno v0.1.0 alpha.

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
models = client.list_openai_models()
policies = client.list_routing_policies()
```

The SDK defaults to bearer token `default`, which works against the default local Docker server. Bearer credentials infer tenant context, so the SDK does not send `x-tenant-guid` unless `tenant_id` is explicitly set. Production deployments should pass an issued bearer token or a globally unique tenant credential value as the bearer token:

```python
client = XenoClient("http://localhost:9000", bearer_token=token)
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

## Interactive Console

```bash
python -m xeno_client.console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK covers authentication, tenants, users, credentials, roles, permissions, grantable permissions, role assignments, clients, advertised models, routing policies, OpenAI-compatible inference, server-collected client telemetry, request history, analytics, settings, backup, health, metrics, and OpenAPI.

The SDK targets the Xeno server API. The `xeno` client local dashboard API,
including local request history, machine telemetry, and provider runtime
telemetry routes, runs on the client machine, uses a per-process local token,
and is documented in
[../../../CLIENT_GETTING_STARTED.md](../../../CLIENT_GETTING_STARTED.md) rather
than wrapped by the server SDKs.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

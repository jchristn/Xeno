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

client = XenoClient("http://localhost:9000", tenant_id="ten_default")
health = client.get_health()
models = client.list_openai_models()
```

Authenticated servers can pass a bearer token:

```python
client = XenoClient("http://localhost:9000", bearer_token=token)
```

## Interactive Console

```bash
python -m xeno_client.console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK covers authentication, tenants, users, credentials, roles, permissions, clients, advertised models, OpenAI-compatible inference, request history, analytics, settings, backup, health, metrics, and OpenAPI.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

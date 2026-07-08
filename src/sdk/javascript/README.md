# Xeno JavaScript SDK

The Xeno JavaScript SDK is a dependency-free ESM client for Xeno v0.1.0 alpha.

## Install

Use it directly from this repository while Xeno is in alpha:

```bash
npm install ./src/sdk/javascript
```

## Quick Start

```javascript
import { XenoClient } from '@xeno-control-plane/sdk';

const client = new XenoClient({
  baseUrl: 'http://localhost:9000'
});

const health = await client.getHealth();
const models = await client.listOpenAiModels();
const policies = await client.listRoutingPolicies();
const leaderboard = await client.getModelTokenLeaderboard({ limit: 10, offset: 0 });
```

The model token leaderboard is global when `tenantId` is omitted. Include
`tenantId` in the filter object or POST body to scope results to one tenant.

The SDK defaults to bearer token `default`, which works against the default local Docker server. Bearer credentials infer tenant context, so the SDK does not send `x-tenant-guid` unless `tenantId` is explicitly set. Production deployments should pass an issued bearer token or a globally unique tenant credential value as the bearer token:

```javascript
const client = new XenoClient({ baseUrl, bearerToken: token });
```

Legacy integrations can still send tenant-bound access credentials through `x-access-key`, but new REST integrations should prefer bearer authentication:

```javascript
const client = new XenoClient({
  baseUrl,
  bearerToken: null,
  tenantId: 'ten_default',
  accessKey
});
```

Use least-privilege credentials with `Inference.Read` and `Inference.Execute`
for application traffic. Credential values may be sent as bearer tokens.
Tenant headers are optional selectors or compatibility hints; they do not
authenticate the request.

## Interactive Console

```bash
npm --prefix src/sdk/javascript run console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK covers authentication, tenants, users, credentials, roles, permissions, grantable permissions, role assignments, clients, advertised models, routing policies, OpenAI-compatible inference, Ollama-compatible inference, server-collected client telemetry, request history, public model token leaderboard, analytics, settings, backup, health, metrics, and OpenAPI.

The SDK targets the Xeno server API. The `xeno` client local dashboard API,
including local request history, machine telemetry, and provider runtime
telemetry routes, runs on the client machine, uses a per-process local token,
and is documented in
[../../../CLIENT_GETTING_STARTED.md](../../../CLIENT_GETTING_STARTED.md) rather
than wrapped by the server SDKs.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

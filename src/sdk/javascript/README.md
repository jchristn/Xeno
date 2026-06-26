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
  baseUrl: 'http://localhost:9000',
  tenantId: 'ten_default'
});

const health = await client.getHealth();
const models = await client.listOpenAiModels();
const policies = await client.listRoutingPolicies();
```

Authenticated servers can pass a bearer token:

```javascript
const client = new XenoClient({ baseUrl, bearerToken: token });
```

## Interactive Console

```bash
npm --prefix src/sdk/javascript run console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK covers authentication, tenants, users, credentials, roles, permissions, clients, advertised models, routing policies, OpenAI-compatible inference, request history, analytics, settings, backup, health, metrics, and OpenAPI.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

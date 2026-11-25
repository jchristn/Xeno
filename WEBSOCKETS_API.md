# Xeno WebSockets API

This document describes the on-the-wire protocol used between a Xeno server and
connected Xeno clients. It is written for client authors, server maintainers,
operators, and developers troubleshooting routed model requests.

Xeno clients keep a persistent WebSocket connection to the server. The server
uses that connection to dispatch OpenAI-compatible requests to selected clients.
Clients execute the request against an Ollama, OpenAI, Gemini, vLLM, or other
OpenAI-compatible provider and return either a buffered response or a streamed
response.

In the platform architecture, this WebSocket is the control-plane link between
the Xeno controller and each resource agent. It allows machines in different
networks, cloud regions, offices, labs, or data center racks to participate in
one model-serving fleet without exposing every provider endpoint directly to
applications. The server authenticates the client, receives inventory, places
work on the connection, and requests telemetry over the same session.

## Protocol Summary

The WebSocket protocol is JSON-over-text frames.

Default endpoint:

```text
ws://localhost:9000/ws/v1/clients
```

TLS deployments should use:

```text
wss://{server}/ws/v1/clients
```

Every text frame is a JSON object with a stable `type` discriminator.

Telemetry collection frames are part of the same authenticated client WebSocket session. The server sends `server.telemetry.request`; clients answer with `client.telemetry.snapshot`, `client.providerTelemetry.snapshot`, `client.telemetry.error`, or `client.providerTelemetry.error`. The server replies with `server.telemetry.ack` after processing the frame.

Telemetry request fields include `requestId`, `tenantId`, `clientId`, `sentUtc`, `collectHostTelemetry`, `collectProviderTelemetry`, `includeRawSnapshot`, `maxRawSnapshotBytes`, `maxProviderMetricRows`, optional history bounds, and `maxSamples`.

Clients must echo the request ID, tenant ID, and client ID. The server rejects telemetry frames whose tenant, client, or WebSocket session does not match the authenticated connection. Telemetry frames never complete inference `server.request` pending responses.

Telemetry payloads must not contain prompts, responses, provider credentials, authorization headers, cookies, API keys, or unbounded raw metrics.

The current protocol version is the Xeno application version:

```text
0.4.1
```

## Authentication

The WebSocket connection is authenticated during the HTTP upgrade request.
The client must send tenant-bound credential headers. User passwords and
dashboard bearer tokens are not valid for client WebSocket authentication.
Bearer dashboard tokens are accepted only on REST routes, never on the client
WebSocket control plane.

Required headers:

| Header | Required | Description |
| --- | --- | --- |
| `x-tenant-guid` | yes | Tenant ID for the client session. |
| `x-access-key` | yes | Client credential access key. |

Alternative tenant header:

| Header | Description |
| --- | --- |
| `x-tenant-id` | Accepted as an alias for `x-tenant-guid`. |

Authentication succeeds only when the access key belongs to the same tenant as
the supplied tenant header. The `x-secret-key` header is rejected and must not be
sent.

If authentication fails, the server sends an error frame when possible and then
closes the socket with a policy violation close status.

Unauthorized frame:

```json
{
  "type": "server.unauthorized",
  "message": "WebSocket authentication requires a client access key."
}
```

Invalid authentication frame:

```json
{
  "type": "server.invalid-auth",
  "message": "Conflicting tenant identifiers were supplied."
}
```

## Connection Lifecycle

1. Client opens WebSocket connection with credential headers.
2. Server authenticates the upgrade request.
3. Server sends `server.connected`.
4. Client sends `client.register` with inventory.
5. Server persists the connected client record and sends `server.registered`.
6. Client sends periodic `client.heartbeat` messages.
7. Server sends `server.heartbeat` acknowledgements.
8. If local provider or model configuration changes while connected, the client
   sends `client.inventory.update` and the server acknowledges with
   `server.inventory.updated`.
9. Server sends `server.request` whenever an API request is routed to the client.
10. Client answers with `client.response` for buffered responses or
   `client.response.start`, `client.response.chunk`, and
   `client.response.complete` for streamed responses.
11. For dashboard model performance tests, the server sends
   `server.performanceTest.request` to one or more clients. Clients answer with
   `client.performanceTest.progress` while samples run and
   `client.performanceTest.response` when a leg completes. Hairpin samples use
   `client.performanceTest.relay.request` and
   `server.performanceTest.relay.response` for each warmup or measured sample.
12. For dashboard model management, the server sends
   `server.modelManagement.request` to a client that advertised an Ollama
   provider with model management enabled. Clients answer with
   `client.modelManagement.progress` while a pull runs and
   `client.modelManagement.response` when the pull or delete completes.
13. On disconnect, the server marks the client disconnected and clears in-flight
    connection state.

## Common Message Rules

All messages are JSON objects.

Every message has:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Stable message discriminator. |

IDs are strings generated by Xeno using a prefix, a k-sortable timestamp
component, and a random component. Backend-generated IDs are no longer than
32 characters.

Timestamps are UTC ISO-8601 strings when present.

Unknown fields should be ignored by receivers. Missing required fields should
be treated as invalid for that message type.

## Server-to-Client Messages

### `server.connected`

Sent immediately after a WebSocket connection is authenticated.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.connected`. |
| `sessionId` | string | yes | Server WebSocket session ID. |
| `tenantId` | string | yes | Authenticated tenant ID. |
| `heartbeatIntervalMs` | number | yes | Server heartbeat expectation. |

Example:

```json
{
  "type": "server.connected",
  "sessionId": "d7a4e4d9-fc2e-4b85-9c2c-7b74320f02a4",
  "tenantId": "ten_default",
  "heartbeatIntervalMs": 15000
}
```

### `server.registered`

Acknowledges a successful client registration.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.registered`. |
| `receivedUtc` | string | yes | Server receive timestamp. |
| `heartbeatIntervalMs` | number | yes | Server heartbeat expectation. |

Example:

```json
{
  "type": "server.registered",
  "receivedUtc": "2026-06-25T18:20:31.274Z",
  "heartbeatIntervalMs": 15000
}
```

### `server.heartbeat`

Acknowledges a client heartbeat.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.heartbeat`. |
| `receivedUtc` | string | yes | Server receive timestamp. |
| `heartbeatIntervalMs` | number | yes | Server heartbeat expectation. |

### `server.inventory.updated`

Acknowledges a client inventory update sent after local provider or model
configuration changes.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.inventory.updated`. |
| `receivedUtc` | string | yes | Server receive timestamp. |
| `heartbeatIntervalMs` | number | yes | Server heartbeat expectation. |

### `server.empty`

Acknowledges an empty client text frame.

### `server.invalid-json`

Indicates the server could not parse a client text frame as JSON.

### `server.invalid-message`

Indicates the server parsed JSON but the frame did not match the required shape.

### `server.unauthorized`

Indicates a message was rejected by tenant or authentication validation.

### `server.request`

Dispatches an API request from the Xeno server to a connected client.

The body field is a JSON string. It contains the exact request body that should
be sent to the upstream provider.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.request`. |
| `requestId` | string | yes | Request ID used for correlation. |
| `tenantId` | string | yes | Authenticated tenant. |
| `requestorTenantId` | string | no | Tenant ID resolved for the authenticated API requestor. |
| `requestorPrincipalType` | string | no | Resolved requestor principal type, such as `User` or `Credential`. |
| `requestorPrincipalId` | string | no | Resolved requestor principal ID. |
| `clientId` | string | yes | Selected client ID. |
| `clientName` | string | no | Selected client name. |
| `apiType` | string | no | Upstream API type selected by routing. |
| `provider` | string | no | Backward-compatible alias for `apiType`. |
| `source` | string | no | Client-local provider endpoint name. |
| `runnerName` | string | no | Backward-compatible alias for `source`. |
| `method` | string | yes | HTTP method to use upstream. |
| `relativePath` | string | yes | Path to send to the upstream provider. |
| `path` | string | no | Backward-compatible alias for `relativePath`. |
| `model` | string | no | Effective model selected by routing. |
| `embeddings` | boolean | yes | True for embedding requests. |
| `stream` | boolean | yes | True when the caller requested streaming. |
| `requestorIp` | string | no | Best-effort original caller IP. |
| `headers` | object | yes | Forwarded request headers. |
| `body` | string | yes | JSON request body to send upstream. |

Forwarded headers normally include:

| Header | Description |
| --- | --- |
| `X-Forwarded-For` | Caller IP chain, with the Xeno caller appended. |
| `X-Real-IP` | Best-effort caller IP. |
| `X-Xeno-Requestor-IP` | Best-effort caller IP for Xeno-aware clients. |
| `X-Xeno-Request-ID` | Xeno request ID. |
| `X-Xeno-Tenant-ID` | Xeno tenant ID. |

Example:

```json
{
  "type": "server.request",
  "requestId": "req_mqtyav17_WK4Y6RpiyDZ6SfDPO16",
  "tenantId": "ten_default",
  "requestorTenantId": "ten_default",
  "requestorPrincipalType": "User",
  "requestorPrincipalId": "usr_mqty7n0j_e64jYPPoJ33J",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "clientName": "THINKPAD",
  "apiType": "Ollama",
  "source": "local-ollama",
  "method": "POST",
  "relativePath": "/v1/chat/completions",
  "model": "llama3.2:latest",
  "embeddings": false,
  "stream": true,
  "requestorIp": "172.18.0.1",
  "headers": {
    "X-Forwarded-For": "172.18.0.1",
    "X-Real-IP": "172.18.0.1",
    "X-Xeno-Requestor-IP": "172.18.0.1",
    "X-Xeno-Request-ID": "req_mqtyav17_WK4Y6RpiyDZ6SfDPO16",
    "X-Xeno-Tenant-ID": "ten_default"
  },
  "body": "{\"model\":\"llama3.2:latest\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}],\"stream\":true}"
}
```

### `server.performanceTest.request`

Asks a connected client to run one model performance benchmark leg.

`mode` controls where requests are executed:

- `direct`: sent to the target client that hosts the model. The client runs
  each sample against its local provider endpoint.
- `relay`: sent to the source client selected in the dashboard. The source
  client creates one `client.performanceTest.relay.request` per sample so the
  request hairpins through the Xeno server to the target client.

The server clamps `requestCount` to 1 through 10 before dispatch.
Completion and chat-completion tests use streaming requests so time-to-first
token can be measured. Embedding tests use non-streaming requests.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.performanceTest.request`. |
| `requestId` | string | yes | Benchmark leg request ID. |
| `tenantId` | string | yes | Tenant ID. |
| `clientId` | string | yes | Recipient client ID. |
| `mode` | string | yes | `direct` or `relay`. |
| `model` | string | yes | Model name to test. |
| `requestType` | string | yes | `chatCompletions`, `completions`, or `embeddings`. |
| `requestCount` | number | yes | Number of samples to run, 1 through 10. |
| `targetClientId` | string | yes | Client that hosts and processes the model. |
| `targetClientName` | string | no | Human-readable target client name. |
| `targetApiType` | string | no | Target provider API type. |
| `targetSource` | string | no | Target client-local provider endpoint name. |
| `timeoutMs` | number | yes | Per-leg timeout in milliseconds. |

Example:

```json
{
  "type": "server.performanceTest.request",
  "requestId": "req_mqtycz54_Xz6cpgzPb7yB",
  "tenantId": "ten_default",
  "clientId": "xcl_source",
  "mode": "relay",
  "model": "gemma3:4b",
  "requestType": "chatCompletions",
  "requestCount": 5,
  "targetClientId": "xcl_gb10",
  "targetClientName": "GB10",
  "targetApiType": "Ollama",
  "targetSource": "local-ollama",
  "timeoutMs": 300000
}
```

### `server.modelManagement.request`

Asks a connected client to run one server-initiated model management operation
against a local Ollama provider that has `modelManagementAllowed` set to `true`
in advertised inventory.

Supported operations:

- `pull`: POSTs to the provider's Ollama `/api/pull` endpoint with streaming
  enabled, forwards progress with `client.modelManagement.progress`, updates
  local `client.json` model inventory on success, and sends
  `client.inventory.update`.
- `delete`: DELETEs the provider's Ollama `/api/delete` endpoint, removes the
  model from local `client.json` model inventory on success, and sends
  `client.inventory.update`.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.modelManagement.request`. |
| `requestId` | string | yes | Model management request ID. |
| `tenantId` | string | yes | Tenant ID. |
| `clientId` | string | yes | Recipient client ID. |
| `operation` | string | yes | `pull` or `delete`. |
| `model` | string | yes | Model name to pull or delete. |
| `source` | string | no | Client-local Ollama provider endpoint name. |
| `insecure` | boolean | no | Passed to Ollama pull to allow insecure registry connections. |
| `timeoutMs` | number | yes | Operation timeout in milliseconds. |

Example:

```json
{
  "type": "server.modelManagement.request",
  "requestId": "req_mqtz7f3c_d4p4pW8Kc9Lq",
  "tenantId": "ten_default",
  "clientId": "xcl_gb10",
  "operation": "pull",
  "model": "gemma3:4b",
  "source": "local-ollama",
  "insecure": false,
  "timeoutMs": 3600000
}
```

### `server.performanceTest.relay.response`

Returns one relayed benchmark sample from the server to the source client.

The server sends this response after it has dispatched the sample to the target
client with a normal `server.request` and received the target client's
`client.response` or streaming response sequence.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.performanceTest.relay.response`. |
| `requestId` | string | yes | Parent benchmark leg request ID. |
| `sampleId` | string | yes | Per-sample request ID. |
| `index` | number | yes | One-based sample index. |
| `success` | boolean | yes | Whether the relayed sample succeeded. |
| `result` | object | yes | Sample timing, token, byte, and status metrics. |
| `error` | string | no | Error text when the sample failed. |

Example:

```json
{
  "type": "server.performanceTest.relay.response",
  "requestId": "req_mqtycz54_Xz6cpgzPb7yB",
  "sampleId": "d57af9d926a74a8c9e5d4c81b3c70391",
  "index": 1,
  "success": true,
  "result": {
    "index": 1,
    "success": true,
    "statusCode": 200,
    "totalTimeMs": 842.4,
    "timeToFirstTokenMs": 211.6,
    "timeToLastTokenMs": 842.4,
    "tokensPerSecond": 29.68,
    "promptTokens": 19,
    "completionTokens": 25,
    "totalTokens": 44,
    "streamChunkCount": 8,
    "requestBytes": 196,
    "responseBytes": 1280,
    "startedUtc": "2026-07-17T20:15:10.143Z"
  }
}
```

### `server.localInference.response`, `.response.start`, `.response.chunk`, `.response.complete`

Answers a `client.localInference.request`. The server sends either one `server.localInference.response` (a
complete, non-streaming answer or an error) or, for streaming requests, a `server.localInference.response.start`,
any number of `server.localInference.response.chunk` frames, and a final `server.localInference.response.complete`.
The client writes them to the local app that called its local inference listener.

Response and start schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | `server.localInference.response`, `server.localInference.response.start`, or `server.localInference.response.complete`. |
| `requestId` | string | yes | The `requestId` of the client request. |
| `success` | boolean | yes | Whether the upstream request succeeded. |
| `statusCode` | number | no | HTTP status code to return to the local app. |
| `contentType` | string | no | Response content type, for example `application/json` or `text/event-stream`. |
| `headers` | object | no | Safe response headers. The client drops hop-by-hop headers, `Content-Length`, and any `X-Xeno-Route` or `X-Xeno-Fallback` value. |
| `body` | string | no | Response body for non-streaming responses. |
| `error` | string | no | Error message when `success` is false. |

Chunk schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `server.localInference.response.chunk`. |
| `requestId` | string | yes | The `requestId` of the client request. |
| `sequence` | number | yes | Zero-based chunk order. |
| `encoding` | string | no | `base64` (default) or `utf8`. |
| `byteCount` | number | yes | Decoded byte count. |
| `contentBase64` | string | no | Chunk bytes when `encoding` is `base64`. |
| `content` | string | no | Chunk text when `encoding` is `utf8`. |
| `sentUtc` | string | no | When the server sent the chunk. |

## Client-to-Server Messages

### `client.register`

Registers or refreshes a connected client record and advertises provider/model
inventory.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.register`. |
| `clientId` | string | no | Existing client ID. Server assigns one when absent. |
| `tenantId` | string | yes | Tenant ID. Must match the authenticated tenant. |
| `name` | string | no | Human-readable client name. |
| `inventory` | object | yes | Hardware and model inventory. |

Inventory schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `models` | array | no | Direct model list. Usually omitted when providers are supplied. |
| `providers` | array | no | Provider endpoint inventory. |
| `runners` | array | no | Backward-compatible alias for providers. |

Provider source schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | Client-local provider endpoint name. |
| `apiType` | string | yes | Upstream API type: `Ollama`, `OpenAI`, `Gemini`, `vLLM`, or `OpenAICompatible`. |
| `provider` | string | no | Backward-compatible alias for `apiType`. |
| `modelManagementAllowed` | boolean | no | True only for Ollama providers whose local `client.json` allows server-initiated model pull/delete operations. |
| `models` | array | yes | Models exposed by this provider endpoint. |

Model schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | Model name exposed to callers. |
| `supportsCompletions` | boolean | yes | Whether chat/completion/generation requests are supported. |
| `supportsEmbeddings` | boolean | yes | Whether embedding requests are supported. |

Example:

```json
{
  "type": "client.register",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "name": "THINKPAD",
  "inventory": {
    "providers": [
      {
        "name": "local-ollama",
        "apiType": "Ollama",
        "modelManagementAllowed": true,
        "models": [
          {
            "name": "llama3.2:latest",
            "supportsCompletions": true,
            "supportsEmbeddings": false
          },
          {
            "name": "all-minilm",
            "supportsCompletions": false,
            "supportsEmbeddings": true
          }
        ]
      }
    ]
  }
}
```

Server behavior:

- The tenant in the message must match the authenticated tenant.
- If the client ID exists under another tenant, registration is rejected.
- The client record is marked connected.
- Existing advertised models for that client are replaced with the current
  inventory.
- Existing advertised provider sources for that client are replaced with the
  current inventory, including the `modelManagementAllowed` capability.
- Model definitions are created or refreshed for newly advertised model names.

Cross-tenant sharing is not declared by the client. Neither `client.register`
nor `client.inventory.update` carries a sharing field, and the server ignores
any such field if one is sent. Whether a client is a shared cross-tenant routing
target is server-authoritative: on first registration the server seeds the
client's `Shared` value from the owner tenant's `DefaultClientShared` setting,
and on every reconnect or inventory update it preserves the existing per-client
value. The only way to change a client's sharing is the owner-tenant REST route
`POST /api/v1/clients/{id}/sharing` (see [REST_API.md](REST_API.md)). This
prevents a node from self-promoting into the shared pool. See
[MODEL_ROUTING.md](archive/MODEL_ROUTING.md) for the routing eligibility rule.

### `client.heartbeat`

Keeps a registered client marked as alive.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.heartbeat`. |
| `clientId` | string | yes | Registered client ID. |
| `tenantId` | string | yes | Tenant ID. |
| `sentUtc` | string | no | Client send timestamp. |

Example:

```json
{
  "type": "client.heartbeat",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "sentUtc": "2026-06-25T18:22:31.274Z"
}
```

### `client.inventory.update`

Refreshes a connected client's advertised inventory without restarting the
WebSocket connection. The Xeno client sends this after local provider or model
configuration changes, including changes made through the local client
dashboard, `xeno provider`/`xeno model` CLI mutations, or `xeno reload` after
manual `~/.xeno/client.json` edits.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.inventory.update`. |
| `clientId` | string | yes | Registered client ID. |
| `tenantId` | string | yes | Tenant ID. |
| `sentUtc` | string | no | Client send timestamp. |
| `inventory` | object | yes | Updated provider/model inventory using the same schema as `client.register.inventory`. |

Example:

```json
{
  "type": "client.inventory.update",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "sentUtc": "2026-06-25T18:22:41.274Z",
  "inventory": {
    "providers": [
      {
        "name": "local-ollama",
        "apiType": "Ollama",
        "modelManagementAllowed": true,
        "models": [
          {
            "name": "llama3.2:latest",
            "supportsCompletions": true,
            "supportsEmbeddings": false
          }
        ]
      }
    ]
  }
}
```

### `client.response`

Returns a complete buffered response for a `server.request`.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.response`. |
| `requestId` | string | yes | Correlates to `server.request.requestId`. |
| `clientId` | string | yes | Client ID. |
| `tenantId` | string | yes | Tenant ID. |
| `success` | boolean | yes | True when upstream exchange succeeded. |
| `statusCode` | number | no | Upstream HTTP status code. Defaults to 200 or 502. |
| `contentType` | string | no | Upstream content type. Defaults to `application/json`. |
| `headers` | object | no | Upstream response headers. |
| `telemetry` | object | no | Client/upstream telemetry. |
| `body` | string | no | Buffered upstream response body. |
| `error` | string | no | Error text when `success` is false. |

Example:

```json
{
  "type": "client.response",
  "requestId": "req_mqtyav17_WK4Y6RpiyDZ6SfDPO16",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "success": true,
  "statusCode": 200,
  "contentType": "application/json",
  "headers": {
    "Content-Type": "application/json",
    "x-request-id": "upstream-123"
  },
  "telemetry": {
    "totalTimeMs": 381,
    "upstreamTotalMs": 381,
    "timeToFirstTokenMs": 381,
    "requestBytes": 112,
    "responseBytes": 148,
    "transferMode": "Standard",
    "providerRequestId": "upstream-123",
    "metadata": {
      "telemetrySource": "xeno.client",
      "streaming": "false"
    }
  },
  "body": "{\"id\":\"chatcmpl-1\",\"choices\":[]}"
}
```

### `client.response.start`

Starts a streamed response for a `server.request` whose `stream` field is true.
The server sends response headers to the original HTTP caller after this frame.

Schema is the same as `client.response`, except:

- `type` is `client.response.start`.
- `body` is usually omitted or empty.
- Telemetry normally contains header timing; final token counts are reported on
  `client.response.complete` after the provider stream ends.

Example:

```json
{
  "type": "client.response.start",
  "requestId": "req_mqtyav17_WK4Y6RpiyDZ6SfDPO16",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "success": true,
  "statusCode": 200,
  "contentType": "text/event-stream",
  "headers": {
    "Content-Type": "text/event-stream"
  },
  "telemetry": {
    "totalTimeMs": 41,
    "upstreamTotalMs": 41,
    "requestToHeadersMs": 41,
    "streamChunkCount": 0,
    "streamEventCount": 0,
    "transferMode": "Stream"
  }
}
```

### `client.response.chunk`

Sends one streamed byte chunk for an active streamed response.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.response.chunk`. |
| `requestId` | string | yes | Correlates to `server.request.requestId`. |
| `clientId` | string | yes | Client ID. |
| `tenantId` | string | yes | Tenant ID. |
| `sequence` | number | yes | Monotonic sequence for this request. |
| `encoding` | string | yes | `base64` for binary-safe transport. |
| `byteCount` | number | yes | Original chunk length in bytes. |
| `contentBase64` | string | yes | Base64-encoded chunk bytes. |
| `content` | string | no | Legacy plain text chunk alternative. |
| `sentUtc` | string | no | Client send timestamp. |

Example:

```json
{
  "type": "client.response.chunk",
  "requestId": "req_mqtyav17_WK4Y6RpiyDZ6SfDPO16",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "sequence": 1,
  "encoding": "base64",
  "byteCount": 86,
  "contentBase64": "ZGF0YTogeyJjaG9pY2VzIjpbeyJkZWx0YSI6eyJjb250ZW50IjoiSGVsbG8ifX1dfQoK",
  "sentUtc": "2026-06-25T18:23:31.274Z"
}
```

Chunk rules:

- Chunks must be sent after `client.response.start`.
- Chunks should be sent in upstream receive order.
- The server relays chunk bytes to the original HTTP caller immediately.
- Empty chunks are ignored.
- `contentBase64` is preferred because it preserves arbitrary bytes.
- `content` is accepted only for backward compatibility.

### `client.response.complete`

Completes a streamed response.

The server uses this frame to finalize request history, telemetry, captured
headers, captured body, and routing trace metadata. It then sends the final HTTP
chunk to the original caller.

Schema is the same as `client.response`, except:

- `type` is `client.response.complete`.
- `body` should contain the complete captured streamed body when practical.
- Telemetry should contain final token counts and stream timings.

For streamed OpenAI-compatible completion and chat requests, the Xeno client
requests provider usage telemetry by forwarding `stream_options.include_usage = true`
to Ollama, OpenAI, vLLM, and generic OpenAI-compatible providers. Token
capture accepts OpenAI-style `usage.prompt_tokens`,
`usage.completion_tokens`, and `usage.total_tokens`; it also normalizes
Ollama-native `prompt_eval_count` and `eval_count` when those fields are
returned by a provider.

Example:

```json
{
  "type": "client.response.complete",
  "requestId": "req_mqtyav17_WK4Y6RpiyDZ6SfDPO16",
  "clientId": "xcl_mqty9z4a_7jJS1gNtyq7m",
  "tenantId": "ten_default",
  "success": true,
  "statusCode": 200,
  "contentType": "text/event-stream",
  "headers": {
    "Content-Type": "text/event-stream"
  },
  "telemetry": {
    "totalTimeMs": 624,
    "upstreamTotalMs": 624,
    "requestToHeadersMs": 41,
    "timeToFirstTokenMs": 95,
    "totalStreamingTimeMs": 529,
    "promptTokens": 32,
    "completionTokens": 18,
    "totalTokens": 50,
    "streamChunkCount": 6,
    "streamEventCount": 6,
    "requestBytes": 188,
    "responseBytes": 492,
    "transferMode": "Stream",
    "providerRequestId": "upstream-123",
    "metadata": {
      "telemetrySource": "xeno.client",
      "streaming": "true"
    }
  },
  "body": "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\ndata: [DONE]\n\n"
}
```

### `client.modelManagement.progress`

Reports live progress for a `server.modelManagement.request`. Pull operations
forward Ollama's line-delimited pull status as progress frames. Delete
operations may emit only `started` and `completed` progress.

When the dashboard posts `ClientModelPullRequest` with `stream` set to `true`,
or sends `Accept: application/x-ndjson`, the REST response is line-delimited
`ModelManagementProgressEvent` JSON objects converted from these websocket
progress frames.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.modelManagement.progress`. |
| `requestId` | string | yes | Correlates to `server.modelManagement.request.requestId`. |
| `tenantId` | string | yes | Tenant ID. |
| `clientId` | string | yes | Client sending the progress event. |
| `operation` | string | yes | `pull` or `delete`. |
| `model` | string | yes | Model name. |
| `source` | string | no | Client-local Ollama provider endpoint name. |
| `eventType` | string | yes | `started`, `progress`, `completed`, or `error`. |
| `status` | string | no | Ollama status text. |
| `message` | string | yes | Human-readable progress status. |
| `completed` | number | no | Bytes completed when reported by Ollama. |
| `total` | number | no | Total bytes when reported by Ollama. |
| `digest` | string | no | Layer digest when reported by Ollama. |
| `rawLine` | string | no | Raw Ollama progress JSON line. |
| `sentUtc` | string | no | UTC timestamp when the client emitted the frame. |

Example:

```json
{
  "type": "client.modelManagement.progress",
  "requestId": "req_mqtz7f3c_d4p4pW8Kc9Lq",
  "tenantId": "ten_default",
  "clientId": "xcl_gb10",
  "operation": "pull",
  "model": "gemma3:4b",
  "source": "local-ollama",
  "eventType": "progress",
  "status": "pulling 91ab477bec9d",
  "message": "pulling 91ab477bec9d",
  "completed": 734003200,
  "total": 1426063360,
  "digest": "sha256:91ab477bec9d",
  "rawLine": "{\"status\":\"pulling 91ab477bec9d\",\"digest\":\"sha256:91ab477bec9d\",\"total\":1426063360,\"completed\":734003200}",
  "sentUtc": "2026-07-17T20:20:10.143Z"
}
```

### `client.modelManagement.response`

Completes one `server.modelManagement.request`.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.modelManagement.response`. |
| `requestId` | string | yes | Correlates to `server.modelManagement.request.requestId`. |
| `tenantId` | string | yes | Tenant ID. |
| `clientId` | string | yes | Client sending the response. |
| `operation` | string | yes | `pull` or `delete`. |
| `model` | string | yes | Model name. |
| `source` | string | no | Client-local Ollama provider endpoint name. |
| `success` | boolean | yes | Whether the operation succeeded. |
| `statusCode` | number | no | Upstream Ollama HTTP status code. |
| `message` | string | yes | Completion or failure message. |
| `error` | string | no | Error text when the operation failed. |
| `body` | string | no | Captured upstream response body or pull progress lines. |
| `sentUtc` | string | no | UTC timestamp when the client emitted the frame. |

Example:

```json
{
  "type": "client.modelManagement.response",
  "requestId": "req_mqtz7f3c_d4p4pW8Kc9Lq",
  "tenantId": "ten_default",
  "clientId": "xcl_gb10",
  "operation": "pull",
  "model": "gemma3:4b",
  "source": "local-ollama",
  "success": true,
  "statusCode": 200,
  "message": "Ollama model pull completed.",
  "sentUtc": "2026-07-17T20:21:03.511Z"
}
```

### `client.performanceTest.progress`

Reports live progress for one model performance benchmark leg. The dashboard
uses these frames to update status text, sample rows, summaries, and charts
before the final leg response arrives.

Progress frames are optional for protocol compatibility. A client that only
sends `client.performanceTest.response` still completes the benchmark, but the
dashboard cannot show per-sample progress for that leg.

Each client should run one unmeasured warmup request of the requested type before
the measured samples for that leg. Warmup progress should be reported with
`warmupStarted` and `warmupCompleted`. Warmup sample metrics are progress-only:
they are not included in the leg `samples` array, the aggregate `summary`, or
the requested measured sample count.

When the dashboard posts `ModelPerformanceTestRequest` with `stream` set to
`true`, or sends `Accept: application/x-ndjson`, the server responds with
line-delimited `ModelPerformanceTestProgressEvent` JSON objects. Those REST
events include server status updates, these client progress frames, leg
completion events, and one final `completed` event containing the full
`ModelPerformanceTestResponse`.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.performanceTest.progress`. |
| `requestId` | string | yes | Correlates to `server.performanceTest.request.requestId`. |
| `clientId` | string | yes | Client sending the progress event. |
| `tenantId` | string | yes | Tenant ID. |
| `mode` | string | yes | `direct` or `relay`. |
| `eventType` | string | yes | `legStarted`, `warmupStarted`, `warmupCompleted`, `sampleCompleted`, or `legCompleted`. |
| `message` | string | yes | Human-readable progress status. |
| `index` | number | no | One-based measured sample index for sample events. Warmup completion may use `0`. |
| `completedSamples` | number | no | Completed sample count for this leg. |
| `totalSamples` | number | no | Expected sample count for this leg. |
| `sample` | object | no | Completed sample result using the sample result schema below. |
| `summary` | object | no | Current aggregate summary for completed samples. |
| `legResult` | object | no | Complete leg result for `legCompleted`. |
| `sentUtc` | string | no | UTC timestamp when the client emitted the frame. |

Example:

```json
{
  "type": "client.performanceTest.progress",
  "requestId": "req_mqtycz54_Xz6cpgzPb7yB",
  "clientId": "xcl_gb10",
  "tenantId": "ten_default",
  "mode": "direct",
  "eventType": "sampleCompleted",
  "message": "Native sample 1 completed in 615.2 ms, first token 144.8 ms, 40.64 tokens/s.",
  "index": 1,
  "completedSamples": 1,
  "totalSamples": 5,
  "sample": {
    "index": 1,
    "success": true,
    "statusCode": 200,
    "totalTimeMs": 615.2,
    "timeToFirstTokenMs": 144.8,
    "timeToLastTokenMs": 615.2,
    "tokensPerSecond": 40.64
  },
  "summary": {
    "successCount": 1,
    "failureCount": 0,
    "averageTotalTimeMs": 615.2
  },
  "sentUtc": "2026-07-17T20:15:10.143Z"
}
```

### `client.performanceTest.response`

Returns one completed model performance benchmark leg to the server.

The response contains a leg result with per-sample metrics plus aggregate
summary values. Direct/native and hairpin/relay legs use the same shape.

Timing semantics:

- Direct/native samples are measured by the target client around its local
  provider HTTP call.
- Hairpin/relay sample `totalTimeMs` and `timeToLastTokenMs` are measured by
  the source client around the full source-client to Xeno server to target
  client round trip. `timeToFirstTokenMs` is the server-observed time until the
  target side produces the first response byte or streamed token; benchmark
  relay samples return one completed sample result to the source client instead
  of streaming sample chunks back to it.
- Embedding samples are non-streaming, so first-token, last-token, and
  tokens-per-second fields are omitted unless the provider returns compatible
  token telemetry.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.performanceTest.response`. |
| `requestId` | string | yes | Correlates to `server.performanceTest.request.requestId`. |
| `clientId` | string | yes | Client sending the result. |
| `tenantId` | string | yes | Tenant ID. |
| `mode` | string | yes | `direct` or `relay`. |
| `success` | boolean | yes | True when at least one sample succeeded. |
| `result` | object | yes | Leg result containing `name`, `description`, `samples`, and `summary`. |
| `error` | string | no | Error text when the leg failed before samples completed. |

Sample result fields:

| Field | Type | Description |
| --- | --- | --- |
| `index` | number | One-based measured sample index. Warmup samples may use `0` in progress events only. |
| `success` | boolean | Whether the sample succeeded. |
| `statusCode` | number | Provider or relay HTTP status code. |
| `totalTimeMs` | number | Total elapsed time in milliseconds. |
| `timeToFirstTokenMs` | number | Time to first token or first response byte. |
| `timeToLastTokenMs` | number | Time to final token or final response byte. |
| `tokensPerSecond` | number | Completion/output tokens per second when token counts are available. |
| `promptTokens` | number | Prompt token count when reported by the provider. |
| `completionTokens` | number | Completion token count when reported by the provider. |
| `totalTokens` | number | Total token count when reported by the provider. |
| `streamChunkCount` | number | Streaming chunks observed for the sample. |
| `requestBytes` | number | Request body bytes. |
| `responseBytes` | number | Response body bytes. |
| `error` | string | Error text for failed samples. |
| `startedUtc` | string | UTC timestamp when the sample started. |

Summary fields:

| Field | Type | Description |
| --- | --- | --- |
| `successCount` | number | Successful sample count. |
| `failureCount` | number | Failed sample count. |
| `averageTotalTimeMs` | number | Average total response time. |
| `medianTotalTimeMs` | number | Median total response time. |
| `minimumTotalTimeMs` | number | Minimum total response time. |
| `maximumTotalTimeMs` | number | Maximum total response time. |
| `averageTimeToFirstTokenMs` | number | Average time to first token or byte. |
| `medianTimeToFirstTokenMs` | number | Median time to first token or byte. |
| `averageTimeToLastTokenMs` | number | Average time to last token or byte. |
| `averageTokensPerSecond` | number | Average output tokens per second. |
| `averageResponseBytes` | number | Average response body size. |

Example:

```json
{
  "type": "client.performanceTest.response",
  "requestId": "req_mqtycz54_Xz6cpgzPb7yB",
  "clientId": "xcl_gb10",
  "tenantId": "ten_default",
  "mode": "direct",
  "success": true,
  "result": {
    "name": "native",
    "description": "Ran 5 chatCompletions request(s) from the system hosting the model.",
    "samples": [
      {
        "index": 1,
        "success": true,
        "statusCode": 200,
        "totalTimeMs": 615.2,
        "timeToFirstTokenMs": 144.8,
        "timeToLastTokenMs": 615.2,
        "tokensPerSecond": 40.64,
        "completionTokens": 25,
        "totalTokens": 44,
        "streamChunkCount": 8,
        "responseBytes": 1210
      }
    ],
    "summary": {
      "successCount": 5,
      "failureCount": 0,
      "averageTotalTimeMs": 621.7,
      "medianTotalTimeMs": 615.2,
      "averageTimeToFirstTokenMs": 146.1,
      "averageTimeToLastTokenMs": 621.7,
      "averageTokensPerSecond": 40.2,
      "averageResponseBytes": 1210
    }
  }
}
```

### `client.performanceTest.relay.request`

Asks the server to relay one benchmark sample from the source client to the
target client that hosts the model.

The frame is sent only while processing a `server.performanceTest.request` with
`mode` set to `relay`. The server validates that the source client ID, tenant
ID, and WebSocket session match the authenticated connection before dispatching
the sample to the target client.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.performanceTest.relay.request`. |
| `requestId` | string | yes | Parent benchmark leg request ID. |
| `sampleId` | string | yes | Per-sample request ID. |
| `index` | number | yes | One-based sample index. |
| `sourceClientId` | string | yes | Source client that initiated the hairpin sample. |
| `targetClientId` | string | yes | Client that hosts and processes the model. |
| `tenantId` | string | yes | Tenant ID. |
| `targetApiType` | string | no | Target provider API type. |
| `targetSource` | string | no | Target provider endpoint name. |
| `model` | string | yes | Model name. |
| `requestType` | string | yes | `chatCompletions`, `completions`, or `embeddings`. |
| `method` | string | yes | Provider HTTP method, normally `POST`. |
| `relativePath` | string | yes | Provider path such as `/v1/chat/completions` or `/api/chat`. |
| `stream` | boolean | yes | Whether the target request should stream. |
| `headers` | object | yes | Forwarded provider headers. |
| `body` | string | yes | JSON provider request body. |

Example:

```json
{
  "type": "client.performanceTest.relay.request",
  "requestId": "req_mqtycz54_Xz6cpgzPb7yB",
  "sampleId": "d57af9d926a74a8c9e5d4c81b3c70391",
  "index": 1,
  "sourceClientId": "xcl_source",
  "targetClientId": "xcl_gb10",
  "tenantId": "ten_default",
  "targetApiType": "Ollama",
  "targetSource": "local-ollama",
  "model": "gemma3:4b",
  "requestType": "chatCompletions",
  "method": "POST",
  "relativePath": "/api/chat",
  "stream": true,
  "headers": {},
  "body": "{\"model\":\"gemma3:4b\",\"stream\":true,\"messages\":[{\"role\":\"user\",\"content\":\"Reply in one short sentence: latency matters. Sample 1.\"}]}"
}
```

### `client.localInference.request`

Sent by a client's local inference listener when a local app asks for a model the client does not answer from a
provider on the same machine. The server authenticates the frame against the client's WebSocket session, routes it
like an OpenAI-compatible or Ollama-compatible request from that client's user, and answers with
`server.localInference.response` frames.

Since 0.2.0 the client answers models served by its own providers directly and sends only the rest. A request whose
local provider failed before responding, and whose route rule allows falling back to the network, sets
`excludeRequestingClient` so the server does not send it back to the client it came from.

Schema:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.localInference.request`. |
| `requestId` | string | yes | Client-generated request ID used to correlate responses and cancellation. |
| `tenantId` | string | yes | Must match the authenticated WebSocket tenant. |
| `clientId` | string | yes | Must be the client registered on this WebSocket session. |
| `method` | string | yes | HTTP method the local app used. |
| `relativePath` | string | yes | Route the local app called, for example `/v1/chat/completions` or `/api/chat`. |
| `model` | string | no | Requested model. Empty for model lists. |
| `embeddings` | boolean | yes | Whether the request is an embeddings request. |
| `stream` | boolean | yes | Whether the local app asked for a streaming response. |
| `providerFamily` | string | yes | `openai` or `ollama`, from the route the local app called. |
| `requestorIp` | string | no | Loopback address of the local app. |
| `headers` | object | no | Forwarded request headers, without hop-by-hop headers. |
| `body` | string | yes | Request body as the local app sent it. |
| `excludeRequestingClient` | boolean | no | Default `false`. When `true`, the server must not select the sending client. Servers before 0.2.0 ignore it, so a fallback can be routed back to the same client and fail there. |

### `client.localInference.cancel`

Tells the server to stop a `client.localInference.request` because the local app disconnected or the listener's
request timeout elapsed.

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `type` | string | yes | Always `client.localInference.cancel`. |
| `requestId` | string | yes | The request to cancel. |
| `tenantId` | string | yes | Tenant ID. |
| `clientId` | string | yes | Sending client. |
| `reason` | string | yes | `CallerDisconnected` or `RequestTimeout`. |

## Telemetry Object

Telemetry is represented as a stable JSON object and persisted into request
history.

| Field | Type | Description |
| --- | --- | --- |
| `totalTimeMs` | number | End-to-end elapsed time for the measured component. |
| `clientRoundTripMs` | number | Server-to-client round-trip time measured by Xeno server. |
| `upstreamTotalMs` | number | Client-to-provider elapsed time. |
| `requestToHeadersMs` | number | Time until upstream response headers. |
| `timeToFirstTokenMs` | number | Time until first byte or token. |
| `totalStreamingTimeMs` | number | Time from first byte/token to stream completion. |
| `promptTokens` | number | Provider prompt tokens, when reported. |
| `completionTokens` | number | Provider completion tokens, when reported. |
| `totalTokens` | number | Provider total tokens, when reported. |
| `streamChunkCount` | number | Number of `client.response.chunk` frames. |
| `streamEventCount` | number | Count of SSE events or NDJSON lines observed. |
| `requestBytes` | number | Upstream request body size in bytes. |
| `responseBytes` | number | Upstream response body size in bytes. |
| `transferMode` | string | `Standard`, `Stream`, or `Unknown`. |
| `providerRequestId` | string | Upstream request ID if safely exposed by headers. |
| `metadata` | object | Provider-neutral string metadata. |

The server may supplement or override telemetry values it measures directly,
including total duration, request body size, response body size, and client
round-trip time.

## Provider Telemetry Frames

Provider telemetry is currently collected and stored by the Xeno Client local
dashboard. The following websocket frame names are reserved for the future
server-requested telemetry workflow:

| Frame | Direction | Purpose |
| --- | --- | --- |
| `server.providerTelemetry.request` | server to client | Ask a connected client for current or recent provider telemetry. |
| `client.providerTelemetry.snapshot` | client to server | Return a current provider telemetry snapshot. |
| `client.providerTelemetry.history` | client to server | Return bounded provider telemetry history for a requested time range. |
| `client.providerTelemetry.error` | client to server | Report that provider telemetry could not be collected or returned. |
| `server.providerTelemetry.ack` | server to client | Acknowledge receipt of provider telemetry. |

Reserved provider telemetry payloads use named fields only and must not include
provider credentials, prompt content, response content, authorization headers,
or unbounded raw metrics. A request may include requested provider names,
requested model names, a time range, maximum sample count, and an explicit
`includeRawMetrics` flag. Clients must keep raw metrics bounded even when the
server asks for them.

## HTTP Response Trace Headers

When a routed request is returned to the original HTTP caller, the server only
adds `X-Xeno-Request-ID` as a caller-visible Xeno trace header. For new routed
requests, this value is also the persisted request history entry ID.

Detailed routing diagnostics are not returned in HTTP or local inference
response headers. Selected client, candidate clients, excluded clients, routing
policy, load-balancing decision, selection reason, timing, token telemetry, and
timeline are captured in Request History and exposed through the dashboard and
protected Request History APIs.

## Request History Capture

For routed requests, the server persists:

- request ID
- tenant ID
- user ID or credential ID
- source IP
- method and path
- selected client ID
- model
- status code
- created and completed timestamps
- total duration
- redacted request headers
- redacted response headers
- captured request body
- captured response body
- body lengths and truncation flags
- telemetry object
- typed timeline events
- routing decision details

Body capture is truncated when it exceeds the server capture limit.
Sensitive headers are redacted before persistence. Redacted header names include
authorization headers, cookies, `x-secret-key`, `x-token`, and headers ending in
`-key`.

## Timeline Events

Request history timeline stages are typed enum values. Current values include:

| Stage | Meaning |
| --- | --- |
| `Received` | Server received the API request. |
| `ModelResolution` | Effective model was resolved. |
| `ClientInventory` | Client inventory was evaluated. |
| `ClientSelection` | Client was selected. |
| `ClientMissing` | Selected client record was missing. |
| `ClientNotConnected` | Selected client was not connected. |
| `ClientDispatch` | Request was dispatched to the client. |
| `ClientResponseStart` | Client started a streamed response. |
| `ClientResponse` | Client returned a buffered response. |
| `ClientResponseComplete` | Client completed a streamed response. |
| `ClientTimeout` | Client did not respond before timeout. |
| `ResponseHeaders` | Upstream response headers were observed. |
| `FirstToken` | First upstream byte or token was observed. |
| `Completed` | Request completed successfully. |
| `Failed` | Request failed. |
| `Streaming` | Stream was active. |
| `Unknown` | Stage was not recognized. |

Each persisted timeline event contains:

| Field | Type | Description |
| --- | --- | --- |
| `stage` | enum string | Stable stage value. |
| `message` | string | Human-readable message. |
| `timestampUtc` | string | UTC timestamp. |
| `elapsedMs` | number | Milliseconds since the server received the request. |
| `success` | boolean | Whether the stage succeeded. |

## Ordering and Correlation

All frames related to one routed request use the same `requestId`.

Buffered response ordering:

```text
server.request
client.response
```

Streaming response ordering:

```text
server.request
client.response.start
client.response.chunk
client.response.chunk
...
client.response.complete
```

Direct model performance leg ordering:

```text
server.performanceTest.request
client.performanceTest.progress (legStarted)
client.performanceTest.progress (warmupStarted)
client.performanceTest.progress (warmupCompleted)
client.performanceTest.progress (sampleCompleted)
...
client.performanceTest.progress (legCompleted)
client.performanceTest.response
```

Hairpin model performance leg ordering:

```text
server.performanceTest.request
client.performanceTest.progress (legStarted)
client.performanceTest.progress (warmupStarted)
client.performanceTest.relay.request
server.request
client.response.start
client.response.chunk
...
client.response.complete
server.performanceTest.relay.response
client.performanceTest.progress (warmupCompleted)
client.performanceTest.relay.request
server.request
client.response.start
client.response.chunk
...
client.response.complete
server.performanceTest.relay.response
client.performanceTest.progress (sampleCompleted)
...
client.performanceTest.progress (legCompleted)
client.performanceTest.response
```

Model pull ordering:

```text
server.modelManagement.request (pull)
client.modelManagement.progress (started)
client.modelManagement.progress (progress)
...
client.inventory.update
client.modelManagement.progress (completed)
client.modelManagement.response
```

Model delete ordering:

```text
server.modelManagement.request (delete)
client.modelManagement.progress (started)
client.inventory.update
client.modelManagement.progress (completed)
client.modelManagement.response
```

Only one final response is expected per request. The server ignores chunks that
arrive for unknown request IDs.

The client may process multiple server requests concurrently only if its local
implementation supports it. The default CLI processes messages through the
single persistent WebSocket connection and sends responses correlated by
`requestId`.

## Timeouts

The server applies the configured WebSocket request timeout while waiting for:

- the first client response frame
- all streamed chunks
- the final streamed response frame

When the timeout expires, the server records a request history entry with a
timeout outcome, returns an HTTP timeout response to the caller, and clears the
pending response state.

## Error Handling

Client-side upstream failures should be returned as `client.response` or
`client.response.complete` with:

```json
{
  "success": false,
  "statusCode": 502,
  "error": "Human-readable failure details."
}
```

If no status code is supplied:

- successful responses default to HTTP 200
- failed responses default to HTTP 502

Malformed WebSocket frames are acknowledged with an error frame when possible.
Authentication and tenant mismatch failures can close the WebSocket.

## Backward Compatibility

The server currently accepts these compatibility aliases:

| New field | Alias |
| --- | --- |
| `apiType` | `provider` |
| `modelManagementAllowed` | `allowModelManagement` |
| `source` | `runnerName` |
| `relativePath` | `path` |
| `inventory.providers` | `inventory.runners` |

The server also accepts `client.response` as a final response for a request that
was originally marked as streamed. In that case the HTTP response is buffered
instead of streamed.

## Security Notes

- WebSocket clients authenticate with client credentials, not user passwords.
- Client credentials are tenant-bound.
- Successful `client.register` frames persist the authenticated credential's
  `userId`, credential ID, and principal type onto the server-side client
  record as owner context.
- The server rejects registration messages whose tenant ID does not match the
  authenticated tenant.
- The server rejects registration, heartbeat, and inventory update frames when
  the referenced client ID is already bound to a different user.
- Cross-tenant sharing is never set from a WebSocket frame. The server ignores
  any sharing field in `client.register` or `client.inventory.update`; sharing is
  owner-set through REST only.
- Request and response history redacts sensitive headers before persistence.
- Client implementations should not log upstream secrets.
- Operators should use `wss://` in production.
- Clients should validate provider endpoint configuration locally before
  connecting to production Xeno servers.

## Client Console Logging

The official client logs routed requests in this format:

```text
Request {id} received from {ip}: {method} {url} via {proxy}
Completed request {id} for {ip}: {method} {url} via {proxy} (##.##ms)
```

Example:

```text
Request req_mqtzhioy_ckNhBWiqCZ8Kd7u1znO received from 172.18.0.1: POST /v1/chat/completions via gb10
Completed request req_mqtzhioy_ckNhBWiqCZ8Kd7u1znO for 172.18.0.1: POST /v1/chat/completions via gb10 (812.45ms)
```

## Minimal Client Implementation Checklist

A custom client must:

1. Open the configured WebSocket URL with `x-tenant-guid` and `x-access-key`.
2. Wait for `server.connected`.
3. Send `client.register` with tenant ID, client ID if known, name, providers,
   and models.
4. Send `client.heartbeat` periodically.
5. On `server.request`, select the provider endpoint named by `source`, forward
   the method/path/body/headers upstream, and return a correlated response.
6. For `stream: false`, send one `client.response`.
7. For `stream: true`, send `client.response.start`, one or more
   `client.response.chunk` frames, and `client.response.complete`.
8. Include telemetry where available.
9. Reconnect with backoff when disconnected.
10. Preserve the same client ID across restarts unless the user resets the
    client.

## Troubleshooting

No requests reach the client:

- Confirm the client is connected in the dashboard.
- Confirm the client registered the expected model name.
- Confirm the caller's requested model matches an advertised model.
- Confirm the client credential tenant matches the API request tenant.

Client receives requests but upstream fails:

- Check the client provider endpoint URL.
- Check upstream provider credentials.
- Check forwarded `relativePath`; OpenAI-compatible providers normally expect
  `/v1/chat/completions`, `/v1/completions`, or `/v1/embeddings`.
- Check client console completion lines for elapsed time and proxy name.

Streaming appears buffered:

- Confirm request body contains `"stream": true`.
- Confirm the selected upstream provider returns chunked transfer encoding,
  server-sent events, or another streaming response body.
- Confirm the client sends `client.response.start` before chunks.
- Confirm chunks are sent as base64 in `contentBase64`.

Request history lacks telemetry:

- Confirm the client includes a `telemetry` object in `client.response` or
  `client.response.complete`.
- Confirm the stream sends `client.response.complete`; stream telemetry is
  finalized from the complete frame.
- Confirm the server has permission to save platform state to the configured
  database.

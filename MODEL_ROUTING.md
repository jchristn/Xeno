# Xeno Model Routing

Xeno routes OpenAI-compatible completions and embeddings through tenant-scoped routing policies. A routing policy decides which connected clients are eligible for a request and which load-balancing strategy is used to select one of those clients.

Routing is the placement layer for Xeno's federated resource model. Connected clients can represent contributor machines on the public internet, workstations on a private network, or accelerator servers in a data center. Each client advertises models, provider endpoints, and capacity signals; policies turn that changing inventory into predictable application behavior.

For application developers, the result is a stable OpenAI-compatible endpoint. For operators, routing policies are where model names, tenant boundaries, preferred nodes, telemetry requirements, and fallback behavior become enforceable placement rules.

Xeno v0.1.0 is alpha. Policy fields and routing behavior may change before a stable release.

## Telemetry-Aware Policy Fields

Routing policies include telemetry fields that round-trip through the REST API:

- `maxTelemetryAgeMs`: default `30000`, clamped from `1000` to `300000`.
- `telemetryFilters`: metric/operator/value conditions.
- `telemetryRanking`: metric/direction/weight ranking rules.
- `telemetryFallbackMode`: `UseLoadBalancingMode` or `FailClosed`.

The telemetry metric catalog is available at `GET /v1.0/api/client-telemetry/metrics`. Catalog entries include host metrics such as `telemetry.cpu.utilizationPercent`, `telemetry.memory.utilizationPercent`, `telemetry.gpu.available`, and provider metrics such as `provider.queuedRequests`, `provider.latencyP95Ms`, and `provider.cacheUsagePercent`.

Runtime telemetry evaluation is designed to use fresh server-collected samples. Operators can inspect collected data under `CONTROL > Client Telemetry` before authoring policy conditions.

## Policy Object

A routing policy contains:

- `id`: compact PrettyId with the `rpol_` prefix.
- `tenantId`: tenant that owns the policy.
- `name`: operator-facing policy name shown in dashboard request details and trace headers.
- `modelName`: requested/effective model name to match, or `*` to match all models.
- `clientName`: connected client name to match, or `*` to allow all clients that advertise the model.
- `requestTimeoutMs`: request timeout associated with the policy. Values below 1000 are clamped to 1000; values above 3600000 are clamped to 3600000.
- `loadBalancingMode`: one of `LeastRecentlyUsed`, `RoundRobin`, `Random`, `FirstAvailable`, or `Adaptive`.
- `adaptive`: adaptive load-balancing settings: `sampleCount`, `capacityWeight`, `inFlightWeight`, `clientWeight`, and `heartbeatWeight`.
- `isDefault`: true only for the tenant default policy.
- `active`: whether the policy participates in matching. The default policy is always active.
- `createdUtc` and `lastUpdateUtc`: audit timestamps.

## Defaults

Every tenant receives a default routing policy:

```json
{
  "name": "Default Routing Policy",
  "modelName": "*",
  "clientName": "*",
  "requestTimeoutMs": 300000,
  "loadBalancingMode": "LeastRecentlyUsed",
  "active": true,
  "isDefault": true
}
```

The default policy is created during initial system startup and whenever a new tenant is created. Startup migration also backfills the default policy for existing tenants.

The default policy cannot be deleted. Its `modelName`, `clientName`, and `active` values are protected as `*`, `*`, and `true`, but its name, timeout, load-balancing mode, and adaptive settings can be edited.

## Matching Rules

For each routed request:

1. Xeno resolves the effective model.
2. Xeno builds the candidate client list from connected, healthy, tenant-scoped clients that advertise the model and support the request type.
3. Xeno evaluates active policies for the request tenant.
4. Policies match when:
   - `modelName` equals the effective model, case-insensitive, or is `*`.
   - `clientName` equals at least one candidate client name, case-insensitive, or is `*`.
5. The closest match wins:
   - exact model + exact client
   - exact model + `*`
   - `*` + exact client
   - `*` + `*`
6. Ties at the same specificity use the most recently updated policy.
7. If no policy is found, Xeno uses an in-memory fallback equivalent to the default `*/*` LeastRecentlyUsed policy.

After policy matching, an exact `clientName` policy narrows the candidate list to that client name. If that client is disconnected, unhealthy, overloaded, or does not advertise the model, the request fails with `NoEligibleClients`.

## Load Balancing

Policies support the same load-balancing modes exposed by Xeno routing settings:

- `LeastRecentlyUsed`: selects the eligible client least recently chosen for the tenant/model/request-type/policy scope. This is the default.
- `RoundRobin`: rotates through weighted candidate buckets.
- `Random`: chooses from weighted candidate buckets randomly.
- `FirstAvailable`: chooses the first deterministic candidate after sorting by name, weight, and ID.
- `Adaptive`: samples eligible clients and scores them using capacity, in-flight requests, configured client weight, and heartbeat freshness.

Adaptive settings:

```json
{
  "sampleCount": 3,
  "capacityWeight": 40,
  "inFlightWeight": 30,
  "clientWeight": 20,
  "heartbeatWeight": 10
}
```

## REST API

List routing policies:

```http
GET /v1.0/api/routing-policies
```

Create a routing policy:

```http
POST /v1.0/api/routing-policies
Content-Type: application/json

{
  "name": "GPU clients for gpt-oss",
  "modelName": "gpt-oss:120b",
  "clientName": "*",
  "requestTimeoutMs": 300000,
  "loadBalancingMode": "LeastRecentlyUsed",
  "adaptive": {
    "sampleCount": 3,
    "capacityWeight": 40,
    "inFlightWeight": 30,
    "clientWeight": 20,
    "heartbeatWeight": 10
  },
  "active": true
}
```

Update a routing policy:

```http
PUT /v1.0/api/routing-policies/{id}
Content-Type: application/json

{
  "name": "GPU clients for gpt-oss",
  "loadBalancingMode": "Adaptive",
  "requestTimeoutMs": 300000,
  "active": true
}
```

Delete a non-default routing policy:

```http
DELETE /v1.0/api/routing-policies/{id}
```

Legacy aliases are also available under `/api/v1/routingpolicies`.

## Dashboard

Open Dashboard > Routing Policies.

Use Create Routing Policy to add a tenant-scoped rule. Use `*` for model or client wildcard matching. Click a row to edit it. The action menu supports edit, View JSON, and delete. The default policy cannot be deleted.

Request History > Request Details includes routing policy ID, policy name, policy match, policy reason, timeout, load-balancing mode, candidate clients, selected client, and telemetry.

## Trace Headers

Routed model responses include policy trace headers:

- `X-Xeno-Routing-Policy-ID`
- `X-Xeno-Routing-Policy-Name`
- `X-Xeno-Routing-Policy-Match`
- `X-Xeno-Routing-Policy-Reason`
- `X-Xeno-Load-Balancing-Mode`
- `X-Xeno-Selection-Reason`
- `X-Client-ID`

These headers are exposed through CORS so browser-based agent applications can inspect them.

## SDKs

The C#, JavaScript, and Python SDKs expose:

- list routing policies
- create routing policy
- update routing policy
- delete routing policy

See the SDK README files under `src/sdk/`.

## Troubleshooting

If a request returns `NoEligibleClients`, check:

- The policy `modelName` matches the requested/effective model.
- The selected exact `clientName` is connected and healthy.
- The client advertises the model with the right capability: completions or embeddings.
- The client has not exceeded `maxParallelRequests`.
- The client heartbeat is recent.

If a different client was selected than expected, inspect Request History and the `X-Xeno-*` trace headers. They show which policy matched, the candidate set, the load-balancing mode, and the selection reason.

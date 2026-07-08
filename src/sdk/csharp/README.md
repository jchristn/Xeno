# Xeno C# SDK

The Xeno C# SDK is a lightweight management-plane and OpenAI-compatible API client for Xeno v0.1.0 alpha.

## Install

Reference the project directly while Xeno is in alpha:

```powershell
dotnet add <your-project>.csproj reference .\src\sdk\csharp\Xeno.Sdk\Xeno.Sdk.csproj
```

## Quick Start

```csharp
using System.Collections.Generic;
using Xeno.Sdk;

using (XenoClient client = new XenoClient("http://localhost:9000"))
{
    HealthResponse health = await client.GetHealthAsync();
    OpenAiModelList models = await client.ListOpenAiModelsAsync();
    List<RoutingPolicy> policies = await client.ListRoutingPoliciesAsync();
    ModelTokenLeaderboardResponse leaderboard = await client.GetModelTokenLeaderboardAsync(
        new Dictionary<string, string> { ["limit"] = "10", ["offset"] = "0" });
}
```

The model token leaderboard is global when `tenantId` is omitted. Include
`tenantId` in the filter dictionary or POST body to scope results to one tenant.

The SDK defaults to bearer token `default`, which works against the default local Docker server. Bearer credentials infer tenant context, so the SDK does not send `x-tenant-guid` unless `TenantId` is explicitly set. Production deployments should pass an issued bearer token or a globally unique tenant credential value as the bearer token:

```csharp
using (XenoClient client = new XenoClient("http://localhost:9000", bearerToken: token))
{
    HealthResponse health = await client.GetHealthAsync();
}
```

Legacy integrations can still send tenant-bound access credentials through `x-access-key`, but new REST integrations should prefer bearer authentication:

```csharp
using (XenoClient client = new XenoClient(new XenoClientOptions
{
    BaseUrl = "http://localhost:9000",
    BearerToken = null,
    TenantId = "ten_default",
    AccessKey = accessKey
}))
{
    OpenAiModelList models = await client.ListOpenAiModelsAsync();
}
```

Use least-privilege credentials with `Inference.Read` and `Inference.Execute`
for application traffic. Credential values may be sent as bearer tokens.
Tenant headers are optional selectors or compatibility hints; they do not
authenticate the request.

## Interactive Console

```powershell
dotnet run --project .\src\sdk\csharp\Xeno.Sdk.Console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK includes methods for:

- authentication and token helpers;
- tenants, users, credentials, roles, permissions, grantable permissions, role assignments, effective permissions, and audit;
- clients and advertised client models;
- model definitions, configurations, and routing policies;
- OpenAI-compatible models, chat completions, completions, and embeddings;
- Ollama-compatible model listing, chat, generation, and embeddings;
- server-collected client telemetry status, latest samples, manual collection,
  history search, rollups, detail reads, deletes, provider telemetry, and the
  telemetry metric catalog;
- request history, public model token leaderboard, analytics, settings, backup,
  restore, health, metrics, and OpenAPI.

The SDK targets the Xeno server API. The `xeno` client local dashboard API,
including local request history, machine telemetry, and provider runtime
telemetry routes, runs on the client machine, uses a per-process local token,
and is documented in
[../../../CLIENT_GETTING_STARTED.md](../../../CLIENT_GETTING_STARTED.md) rather
than wrapped by the server SDKs.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

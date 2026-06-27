# Xeno C# SDK

The Xeno C# SDK is a lightweight management-plane and OpenAI-compatible API client for Xeno v0.1.0 alpha.

## Install

Reference the project directly while Xeno is in alpha:

```powershell
dotnet add <your-project>.csproj reference .\src\sdk\csharp\Xeno.Sdk\Xeno.Sdk.csproj
```

## Quick Start

```csharp
using Xeno.Sdk;

using (XenoClient client = new XenoClient("http://localhost:9000", tenantId: "ten_default"))
{
    XenoApiResponse health = await client.GetHealthAsync();
    XenoApiResponse models = await client.ListOpenAiModelsAsync();
    XenoApiResponse policies = await client.ListRoutingPoliciesAsync();
}
```

Authenticated servers can use bearer tokens:

```csharp
using (XenoClient client = new XenoClient("http://localhost:9000", bearerToken: token))
{
    XenoApiResponse health = await client.GetHealthAsync();
}
```

## Interactive Console

```powershell
dotnet run --project .\src\sdk\csharp\Xeno.Sdk.Console
```

Type `?` for commands. The console can inspect health, clients, tenants, users, credentials, roles, permissions, settings, metrics, request history, and send a chat request.

## Coverage

The SDK includes methods for:

- authentication and token helpers;
- tenants, users, credentials, roles, permissions, effective permissions, and audit;
- clients and advertised client models;
- model definitions, configurations, and routing policies;
- OpenAI-compatible models, chat completions, completions, and embeddings;
- request history, analytics, settings, backup, restore, health, metrics, and OpenAPI.

The SDK targets the Xeno server API. The `xeno` client local dashboard API runs
on the client machine, uses a per-process local token, and is documented in
[../../../CLIENT_GETTING_STARTED.md](../../../CLIENT_GETTING_STARTED.md) rather
than wrapped by the server SDKs.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

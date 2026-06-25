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

using XenoClient client = new XenoClient("http://localhost:9000", tenantId: "ten_default");
using JsonDocument health = await client.GetHealthAsync();
using JsonDocument models = await client.ListOpenAiModelsAsync();
```

Authenticated servers can use bearer tokens:

```csharp
using XenoClient client = new XenoClient("http://localhost:9000", bearerToken: token);
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
- model definitions and configurations;
- OpenAI-compatible models, chat completions, completions, and embeddings;
- request history, analytics, settings, backup, restore, health, metrics, and OpenAPI.

## Alpha Notice

Xeno v0.1.0 is alpha software. API shapes may change before a stable release.

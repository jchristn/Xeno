# Testing the Xeno C# SDK

Run the SDK tests:

```powershell
dotnet test .\src\sdk\csharp\Xeno.Sdk.Tests\Xeno.Sdk.Tests.csproj
```

The test suite verifies request construction for management APIs, OpenAI-compatible APIs, request history, and server-collected client telemetry routes.

Run the interactive console:

```powershell
dotnet run --project .\src\sdk\csharp\Xeno.Sdk.Console
```

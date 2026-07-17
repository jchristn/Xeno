# AGENTS.md

Follow `C:\code\agents\requirements` when editing this repository.

## Build

```powershell
dotnet build src/Xeno.sln
dotnet run --project src/Test.Automated -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

## Style

- No `var`.
- No tuple returns.
- `using` statements belong inside namespace blocks.
- Public APIs need XML documentation.
- Async methods that perform cancellable work accept `CancellationToken`.
- Use `ConfigureAwait(false)` in library/server code.

## Testing

Shared test descriptors live in `src/Test.Shared` and run through Touchstone:

- `src/Test.Automated`
- `src/Test.Xunit`
- `src/Test.Nunit`

## Local Inference Server

- The Xeno Client local inference listener is configured by `ClientSettings.LocalInferenceServer` in `~/.xeno/client.json`.
- Defaults are disabled, loopback host `127.0.0.1`, port `11434`, and CORS enabled for local app integration.
- Loading existing client settings must backfill missing `LocalInferenceServer` defaults and rewrite `client.json`.
- The listener is implemented under `src/Xeno.Client/LocalInference` as a Watson7 service and runtime separate from the local dashboard.
- The listener must remain loopback-only. Accept `localhost`, `127.0.0.1`, `::1`, and `[::1]`; reject wildcard, public, and non-local DNS hosts.
- Supported local APIs are only the OpenAI-compatible `/v1/chat/completions`, `/v1/embeddings`, `/v1/models`, `/v1/models/{model}` routes and the Ollama-compatible `/api/generate`, `/api/chat`, `/api/embeddings`, `/api/embed`, `/api/tags`, `/api/show`, `/api/ps` routes.
- Local requests are proxied through typed websocket messages in `src/Xeno.Core/Models`; preserve cancellation, streaming chunks, status codes, content types, and safe response headers from the Xeno server.
- When the websocket tunnel is unavailable, disconnected, severed, or unauthorized, local endpoints return provider-shaped 502 responses.
- Use named DTO classes for local inference requests and responses. Do not inspect request payloads with `JsonNode`, `JsonArray`, `JsonElement`, `JsonObject`, or JSON string contains checks; `System.Text.Json` serialization to and from named types is allowed.
- Prompt, chat, and embedding input content must not be logged unless an explicit existing diagnostic setting permits it.
- Keep `xeno status`, client state, local dashboard settings, the local dashboard API Explorer, Postman, and markdown documentation in sync with local inference behavior.
- Cover local inference changes in `src/Test.Shared` whenever behavior is product-facing so the automated, Xunit, and NUnit harnesses all exercise it.

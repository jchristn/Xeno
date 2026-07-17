# Local Inference Server Plan

## Status

Goal: add an optional Xeno.Client background service that exposes localhost-only OpenAI-compatible and Ollama-compatible HTTP APIs, proxies requests through the existing Xeno websocket tunnel, and adds matching dashboard, documentation, test, and Postman coverage.

Current state:

- [ ] Not started
- [ ] In progress
- [x] Complete

Validation completed:

- `dotnet build src/Xeno.sln` passed with 0 warnings.
- `dotnet run --project src/Test.Automated -- --results results.json` passed 119/119.
- `dotnet test src/Test.Xunit` passed 119/119.
- `dotnet test src/Test.Nunit` passed 119/119.
- `npm.cmd --prefix dashboard test` passed.
- `npm.cmd --prefix dashboard run build:local` passed.
- `npm.cmd --prefix dashboard run build` passed.
- Executable smoke test started `xeno connect --foreground` with `LocalInferenceServer.Enabled=true` on an isolated loopback port and confirmed OpenAI/Ollama provider-shaped 502 responses while the websocket was disconnected.

## Product Decisions

- [x] The feature is disabled by default.
- [x] The listener is localhost-only. It is configurable in `~/.xeno/client.json`, but it rejects non-loopback hosts.
- [x] The listener starts automatically when enabled and does not depend on the local dashboard being enabled.
- [x] The service uses the configured client identity from `client.json`, including `ClientId`, `TenantId`, and related existing identity fields.
- [x] The service returns 502-compatible provider-shaped errors when the websocket is disconnected, severed, or unavailable.
- [x] Streaming is supported for compatible OpenAI and Ollama generation/chat endpoints.
- [x] Request bodies are amended, where possible, to request telemetry, token counts, timings, and statistics from the Xeno server response.
- [x] Model listing returns all models returned by the Xeno server.
- [x] Ollama `/api/ps` returns all active/running models known to the Xeno server, behaving as a shared large Ollama server.
- [x] Unsupported endpoints return response bodies and HTTP status codes matching the detected provider family as closely as possible.
- [x] CORS is supported for localhost app integration.
- [x] The dashboard API Explorer provides executable sample requests and always targets the local inference server.
- [x] Postman adds `Xeno Client > Local Ollama APIs` and `Xeno Client > Local OpenAI APIs`.

## Compatibility Scope

### OpenAI-Compatible APIs

- [x] `POST /v1/chat/completions`
  - [x] Accept OpenAI-style chat completion requests with named DTOs.
  - [x] Accept a single chat request per HTTP call.
  - [x] Support non-streaming JSON responses.
  - [x] Support streaming `text/event-stream` responses with OpenAI-style `chat.completion.chunk` payloads and terminal `data: [DONE]`.
  - [x] Include provider-compatible usage fields when available.
  - [x] Return OpenAI-shaped errors for validation failures, upstream errors, websocket failures, and unsupported options.
- [x] `POST /v1/embeddings`
  - [x] Accept `input` as a single string or an array of strings.
  - [x] Return OpenAI-style embedding list responses with deterministic `index` values.
  - [x] Include usage fields when available.
  - [x] Return OpenAI-shaped errors for invalid input and upstream failures.
- [x] `GET /v1/models`
  - [x] Return all Xeno server-visible models in OpenAI `list` format.
  - [x] Preserve enough metadata for client compatibility without exposing Xeno-only internals in unexpected fields.
- [x] `GET /v1/models/{model}`
  - [x] Return one OpenAI-style model object when the model is known.
  - [x] Return OpenAI-style 404 errors when the model is unknown.
- [x] Unsupported `/v1/*` endpoints
  - [x] Return OpenAI-shaped error responses with appropriate status codes.
  - [x] Do not expose stack traces or internal transport details.

### Ollama-Compatible APIs

- [x] `POST /api/generate`
  - [x] Accept Ollama text generation requests with named DTOs.
  - [x] Accept a single generation request per HTTP call.
  - [x] Support Ollama default streaming newline-delimited JSON.
  - [x] Support `stream: false` single JSON response.
  - [x] Include Ollama-compatible timing, token, and count fields when available.
  - [x] Return Ollama-shaped errors for validation failures, upstream errors, websocket failures, and unsupported options.
- [x] `POST /api/chat`
  - [x] Accept Ollama chat requests with named DTOs.
  - [x] Accept a single chat request per HTTP call.
  - [x] Support Ollama default streaming newline-delimited JSON.
  - [x] Support `stream: false` single JSON response.
  - [x] Include Ollama-compatible timing, token, and count fields when available.
- [x] `POST /api/embeddings`
  - [x] Support legacy Ollama embeddings shape with `prompt`.
  - [x] Return the legacy single `embedding` response shape.
  - [x] Keep this DTO separate from the current `/api/embed` DTO.
- [x] `POST /api/embed`
  - [x] Support current Ollama embedding shape with `input` as a string or string array.
  - [x] Return the current `embeddings` response shape.
  - [x] Include timing fields when available.
- [x] `GET /api/tags`
  - [x] Return all Xeno server-visible models in Ollama tag format.
- [x] `POST /api/show`
  - [x] Return Ollama-compatible model detail responses.
  - [x] Return Ollama-shaped 404 errors for unknown models.
- [x] `GET /api/ps`
  - [x] Return all active/running models known to the Xeno server.
  - [x] Treat the Xeno server as a shared Ollama runtime rather than only this local process.
- [x] Unsupported `/api/*` endpoints
  - [x] Return Ollama-shaped error responses with appropriate status codes.
  - [x] Do not expose stack traces or internal transport details.

### Explicit Non-Goals

- [x] Do not implement OpenAI JSONL batch APIs.
- [x] Do not implement vLLM-specific APIs.
- [x] Do not add authentication to the local listener.
- [x] Do not bind to public interfaces.
- [x] Do not require the local dashboard to be enabled.

## Configuration

- [x] Add a new client settings section, likely `LocalInferenceServer`.
- [x] Add a dedicated settings model in Xeno.Client, for example `ClientLocalInferenceServerSettings`.
- [x] Default values:
  - [x] `Enabled`: `false`
  - [x] `Host`: `127.0.0.1`
  - [x] `Port`: `11434`
  - [x] `EnableCors`: `true`
  - [x] `CorsAllowedOrigins`: localhost-only defaults, or wildcard only when safely limited to loopback binding
  - [x] Optional request/response limits consistent with existing Xeno.Client conventions
- [x] Update `ClientSettings.Load()` so loading an existing `client.json` backfills missing defaults and rewrites the file.
- [x] Preserve existing user-provided settings and formatting behavior as much as the current serializer allows.
- [x] Validate that the configured host is loopback only:
  - [x] Accept `localhost`.
  - [x] Accept `127.0.0.1`.
  - [x] Accept `::1` and `[::1]`.
  - [x] Reject `0.0.0.0`, public IPs, wildcard hosts, and DNS names that are not explicitly localhost.
- [x] Add settings display and edit support wherever current client settings are shown or changed.
- [x] Document the new `client.json` section and the default rewrite behavior.

## Architecture

- [x] Add a standalone Xeno.Client service namespace, `Xeno.Client.LocalInference`.
- [x] Build the service with Watson7 using the same project patterns as the local dashboard service.
- [x] Run this as a separate background service from the local dashboard.
- [x] Add `LocalInferenceServerRuntime`, parallel to `LocalDashboardRuntime`.
- [x] Start the runtime during client startup when `LocalInferenceServer.Enabled` is true.
- [x] Stop and dispose the runtime cleanly during client shutdown.
- [x] Log local inference server startup attempts through existing Xeno.Client logging patterns.
- [x] Log startup failures with actionable details, including configured host, configured port, exception type, and likely remediation.
- [x] Detect and clearly log port-level bind conflicts so the user can change `LocalInferenceServer.Port`.
- [x] Keep the Xeno.Client process running if the local inference listener fails to start.
- [x] Add client connection state fields for local inference status:
  - [x] Enabled
  - [x] Listening
  - [x] URL
  - [x] Last error
  - [x] Startup timestamp
  - [x] Last request timestamp
- [x] Ensure the service can report healthy listener state even when the websocket is currently disconnected.
- [x] Ensure request handling fails per-provider with 502 when the websocket cannot service the request.

## Watson7 Service Design

- [x] Create the Watson webserver with configured loopback host and port.
- [x] Register explicit routes for each supported OpenAI and Ollama endpoint.
- [x] Register preflight handling for CORS.
- [x] Add Swagger/OpenAPI metadata for every supported route.
- [x] Expose the OpenAPI document and Swagger UI only on the local listener.
- [x] Keep route handlers thin and delegate provider behavior to focused services.
- [x] Implement request size, method, and content-type validation with provider-compatible errors.
- [x] Tolerate common client headers such as `Authorization`, `OpenAI-Organization`, and Ollama client headers without requiring auth.
- [x] Return provider-compatible 404s for unsupported routes, not Watson default HTML responses.
- [x] Use cancellation tokens throughout request handling.
- [x] Use `ConfigureAwait(false)` in library/server async code.

## DTO and Serialization Requirements

- [x] Use named DTO classes for all request and response payloads.
- [x] Do not inspect application payloads through `JsonNode`, `JsonObject`, `JsonArray`, or `JsonElement`.
- [x] Avoid string-contains checks against JSON.
- [x] Add custom converters only where protocol compatibility requires union shapes, such as:
  - [x] OpenAI embeddings `input` string or string array.
  - [x] Ollama `/api/embed` `input` string or string array.
  - [x] Provider options dictionaries if unavoidable.
- [x] Prefer strongly named option DTOs for known provider options.
- [x] Keep DTOs one class per file where practical, following repository style.
- [x] Use explicit property names and `JsonPropertyName` attributes where wire casing differs from C# names.
- [x] Add XML documentation for public DTOs and public service APIs.
- [x] Ensure serialization omits nulls only where the provider API expects omission.

## Websocket Proxy Contract

- [x] Identify the existing websocket request path used by Xeno.Client to ask the Xeno server for model, embedding, chat, and completion work.
- [x] Add or reuse typed client-to-server messages for:
  - [x] Model list
  - [x] Model details
  - [x] Running/active model list
  - [x] Text generation
  - [x] Chat completion
  - [x] Embeddings
- [x] Include the configured client identity and tenant context in proxied requests.
- [x] Include telemetry/statistics request flags when the upstream contract supports them.
- [x] Add streaming response support through the websocket tunnel.
- [x] Preserve request cancellation from the local HTTP client through websocket cancellation where supported.
- [x] Map upstream provider/model errors into OpenAI or Ollama response shapes.
- [x] Map transport loss, authorization failure, or disconnected state into provider-shaped 502 responses.
- [x] Propagate Xeno Server HTTP response headers that are returned through the websocket onto localhost responses from the local inference server.
- [x] Exclude hop-by-hop, invalid, duplicate-unsafe, and listener-owned headers from propagation, including `Connection`, `Transfer-Encoding`, `Keep-Alive`, `Upgrade`, `Proxy-Authenticate`, `Proxy-Authorization`, `TE`, `Trailer`, and local `Content-Length`.
- [x] Preserve provider-relevant headers such as request IDs, rate-limit metadata, model/provider metadata, cache metadata, and server timing when supplied by Xeno Server.
- [x] Define deterministic behavior when propagated headers conflict with required OpenAI/Ollama compatibility headers.
- [x] Ensure concurrent HTTP requests can safely share the websocket tunnel.
- [x] Add backpressure or bounded buffering for streaming responses.

## Streaming Behavior

- [x] OpenAI chat streaming:
  - [x] Use `Content-Type: text/event-stream`.
  - [x] Emit `data: {json}` events for chunks.
  - [x] Emit `data: [DONE]` when complete.
  - [x] Flush after chunks where Watson7 supports it.
  - [x] Map upstream stream errors to an OpenAI-style SSE error event when possible, otherwise terminate cleanly.
- [x] Ollama generate/chat streaming:
  - [x] Use newline-delimited JSON.
  - [x] Match Ollama `done: false` chunk behavior.
  - [x] Emit final `done: true` object with timing and count fields when available.
  - [x] Support `stream: false` aggregation into a single final response.
- [x] Embeddings and model APIs remain non-streaming.
- [x] Add tests that validate streaming framing, termination, and error mapping.

## Error Mapping

- [x] Implement provider-specific error factories.
- [x] OpenAI error shape:
  - [x] Include `error.message`.
  - [x] Include `error.type`.
  - [x] Include `error.param` where applicable.
  - [x] Include `error.code` where applicable.
  - [x] Use OpenAI-compatible statuses for 400, 404, 405, 413, 415, 422, 429, 500, and 502.
- [x] Ollama error shape:
  - [x] Include `error`.
  - [x] Use Ollama-compatible statuses for 400, 404, 405, 413, 415, 500, and 502.
- [x] Detect provider family by route prefix:
  - [x] `/v1/*` uses OpenAI-compatible errors.
  - [x] `/api/*` uses Ollama-compatible errors.
  - [x] Unknown prefixes use a small JSON error without internal details.
- [x] Log local errors through existing Xeno.Client logging patterns.
- [x] Do not log prompt or embedding input content unless an existing opt-in diagnostic setting explicitly allows it.

## Dashboard API Explorer

- [x] Add a local dashboard page for the local inference API explorer.
- [x] Keep visual structure and behavior aligned with the server dashboard API Explorer.
- [x] Show only Local OpenAI APIs and Local Ollama APIs.
- [x] Always target the local inference server base URL.
- [x] Show enabled/listening/error status from local client state.
- [x] Provide executable examples for:
  - [x] OpenAI chat completions
  - [x] OpenAI streaming chat completions
  - [x] OpenAI embeddings
  - [x] OpenAI model list
  - [x] OpenAI model details
  - [x] Ollama generate
  - [x] Ollama streaming generate
  - [x] Ollama chat
  - [x] Ollama streaming chat
  - [x] Ollama legacy embeddings
  - [x] Ollama current embed
  - [x] Ollama tags
  - [x] Ollama show
  - [x] Ollama ps
- [x] Include copyable curl requests.
- [x] Include editable request bodies where the server dashboard API Explorer pattern supports it.
- [x] Execute requests from the browser and render provider-shaped responses.
- [x] Handle disabled listener and websocket unavailable cases clearly.
- [x] Add settings visibility for the local inference server configuration.
- [x] Update static fallback dashboard assets if the project still relies on embedded fallback HTML/JS.

## CLI and Status Output

- [x] Include local inference server details in `xeno status`.
- [x] Include local inference server details in any related client status, diagnostics, or settings commands where local dashboard details are already shown.
- [x] Show whether the local inference server is enabled.
- [x] Show whether the local inference server is listening.
- [x] Show the configured local inference server URL when enabled.
- [x] Show startup or listener errors when present, including port conflict details.
- [x] Show websocket availability impact clearly, including that local HTTP endpoints will return provider-shaped 502 responses when the tunnel is unavailable.
- [x] Keep disabled output concise so default installations are not noisy.
- [x] Add tests or command-output snapshots for updated CLI/status output where the repository has coverage for command behavior.

## Postman

- [x] Update `Xeno.postman_collection.json`.
- [x] Add collection variable `localInferenceBaseUrl`, defaulting to `http://127.0.0.1:11434`.
- [x] Under `Xeno Client`, add folder `Local OpenAI APIs`.
- [x] Under `Xeno Client`, add folder `Local Ollama APIs`.
- [x] Add executable OpenAI requests:
  - [x] Chat completions
  - [x] Streaming chat completions where Postman behavior supports it
  - [x] Embeddings with single input
  - [x] Embeddings with batch input
  - [x] List models
  - [x] Retrieve model
- [x] Add executable Ollama requests:
  - [x] Generate
  - [x] Generate streaming where Postman behavior supports it
  - [x] Chat
  - [x] Chat streaming where Postman behavior supports it
  - [x] Legacy embeddings
  - [x] Embed single input
  - [x] Embed batch input
  - [x] Tags
  - [x] Show
  - [x] Ps
- [x] Add brief request descriptions explaining local listener requirements.

## Documentation

- [x] Update markdown files that introduce or operate Xeno.Client:
  - [x] `CLIENT_GETTING_STARTED.md`
  - [x] `README.md`
  - [x] `getting-started/WINDOWS_CLIENT.md`
  - [x] `getting-started/MACOS_CLIENT.md`
  - [x] `getting-started/LINUX_CLIENT.md`
- [x] Update user-facing client documentation to describe:
  - [x] Purpose of the local inference server.
  - [x] How to enable it in `~/.xeno/client.json`.
  - [x] Default host and port.
  - [x] Loopback-only binding behavior.
  - [x] No local auth requirement.
  - [x] Websocket dependency and 502 behavior.
  - [x] OpenAI-compatible base URL.
  - [x] Ollama-compatible base URL.
  - [x] Streaming support.
  - [x] Supported and unsupported endpoints.
- [x] Update developer documentation for:
  - [x] Service architecture.
  - [x] DTO conventions.
  - [x] Websocket proxy message contracts.
  - [x] Testing strategy.
- [x] Update release notes or changelog if the repository uses one.

## Tests

- [x] Add unit tests for settings load/backfill/save behavior.
- [x] Add unit tests for loopback host validation.
- [x] Add unit tests for local inference startup failure logging.
- [x] Add unit tests for port conflict handling and connection state updates.
- [x] Add unit tests for route matching and unsupported endpoint provider detection.
- [x] Add unit tests for OpenAI request validation.
- [x] Add unit tests for Ollama request validation.
- [x] Add unit tests for OpenAI error response mapping.
- [x] Add unit tests for Ollama error response mapping.
- [x] Add unit tests for Xeno Server response header propagation and excluded hop-by-hop headers.
- [x] Add unit tests for websocket disconnected and unauthorized 502 mapping.
- [x] Add unit tests for OpenAI embeddings single and batch inputs.
- [x] Add unit tests for Ollama `/api/embeddings` legacy input.
- [x] Add unit tests for Ollama `/api/embed` single and batch inputs.
- [x] Add integration tests using an in-process or loopback local inference server with a fake typed websocket proxy.
- [x] Add integration tests for OpenAI non-streaming chat completions.
- [x] Add integration tests for OpenAI streaming chat completions.
- [x] Add integration tests for Ollama non-streaming generate.
- [x] Add integration tests for Ollama streaming generate.
- [x] Add integration tests for Ollama non-streaming chat.
- [x] Add integration tests for Ollama streaming chat.
- [x] Add integration tests for model list, model details, tags, show, and ps.
- [x] Add CORS preflight tests.
- [x] Add concurrency tests for multiple simultaneous local HTTP requests.
- [x] Add cancellation tests for a client-disconnected streaming request.
- [x] Add shared Touchstone descriptors where the behavior is product-facing and should run across automated, Xunit, and NUnit harnesses.
- [x] Add dashboard tests for the API Explorer page if the repository has a dashboard test harness.
- [x] Add Postman collection structural validation if an existing collection validation step exists.

## Validation Commands

Run these before marking the implementation complete:

- [x] `dotnet build src/Xeno.sln`
- [x] `dotnet run --project src/Test.Automated -- --results results.json`
- [x] `dotnet test src/Test.Xunit`
- [x] `dotnet test src/Test.Nunit`
- [x] Dashboard build/test command used by this repository, if present.
- [x] Manual smoke test with `LocalInferenceServer.Enabled=true`.
- [x] Manual OpenAI smoke test against the configured loopback listener `/v1/models` route.
- [x] Manual Ollama smoke test against the configured loopback listener `/api/tags` route.
- [x] Manual websocket-disconnected smoke test confirming provider-shaped 502 responses.

## Implementation Order

- [x] Phase 1: Configuration and lifecycle
  - [x] Add settings DTO and defaults.
  - [x] Add load/backfill/save support.
  - [x] Add lifecycle runtime and connection state.
  - [x] Add loopback host validation.
- [x] Phase 2: DTOs and provider protocol layer
  - [x] Confirmed the existing websocket server request path supports method, relative path, headers, request body, model, embeddings, and streaming flags.
  - [x] Decided to add a typed client-initiated local inference websocket request/response protocol so localhost requests are serviced through the existing Xeno server tunnel.
  - [x] Add OpenAI DTOs.
  - [x] Add Ollama DTOs.
  - [x] Add provider-specific error factories.
  - [x] Add input union converters without raw JSON inspection.
- [x] Phase 3: Watson7 listener
  - [x] Add service class and route registration.
  - [x] Add CORS and preflight support.
  - [x] Add OpenAPI/Swagger metadata.
  - [x] Add unsupported route handling.
- [x] Phase 4: Websocket proxy
  - [x] Reuse or add typed websocket messages.
  - [x] Add model, running-model, generation, chat, and embedding proxy operations.
  - [x] Add streaming bridge and cancellation handling.
  - [x] Add telemetry/statistics request flags where existing upstream client handling already requests/captures usage and telemetry.
- [x] Phase 5: Dashboard
  - [x] Add local API Explorer page.
  - [x] Add executable samples and response rendering.
  - [x] Add status/settings visibility.
  - [x] Update fallback static dashboard path if required.
- [x] Phase 6: Postman and docs
  - [x] Add Postman folders and requests.
  - [x] Update user docs.
  - [x] Update developer docs and release notes.
- [x] Phase 7: Tests and validation
  - [x] Add unit tests.
  - [x] Add integration tests.
  - [x] Add Touchstone descriptors where appropriate.
  - [x] Run the full validation command set.
  - [x] Fix all compiler warnings and test failures.
- [x] Phase 8: Repository agent guidance
  - [x] Edit `AGENTS.md` as the final implementation step.
  - [x] Make `AGENTS.md` complete for the new local inference server architecture, configuration, testing, documentation, and dashboard expectations.
  - [x] Verify the final `AGENTS.md` guidance matches the implemented behavior and does not contradict `C:\code\agents\requirements`.

## Acceptance Criteria

- [x] A fresh `client.json` includes `LocalInferenceServer` defaults with the feature disabled.
- [x] An existing `client.json` is automatically rewritten after load to include missing defaults.
- [x] Enabling the feature starts a Watson7 local listener independently of the dashboard.
- [x] The listener rejects non-loopback host configuration.
- [x] OpenAI-compatible clients can list models, retrieve a model, request embeddings, and request chat completions.
- [x] Ollama-compatible clients can list tags, show a model, list running models, request embeddings, generate text, and chat.
- [x] Streaming works for OpenAI chat completions and Ollama generate/chat.
- [x] When the websocket is unavailable, supported endpoints return provider-shaped 502 responses.
- [x] Unsupported OpenAI and Ollama paths return provider-shaped errors.
- [x] No API handler uses raw JSON DOM peeking for normal request processing.
- [x] The local dashboard exposes an executable API Explorer for only these local OpenAI and Ollama APIs.
- [x] Postman includes executable Local OpenAI and Local Ollama folders under Xeno Client.
- [x] Documentation explains configuration, supported APIs, streaming, and failure behavior.
- [x] Full repository validation passes without compiler warnings.

## Notes for Implementers

- Follow `C:\code\agents\requirements` and repository `AGENTS.md` rules.
- Keep `using` statements inside namespace blocks.
- Do not use `var`.
- Do not return tuples.
- Add XML documentation to public APIs.
- Accept `CancellationToken` in async methods that perform cancellable work.
- Use `ConfigureAwait(false)` in library/server async code.
- Do not use `System.Text.Json` DOM types such as `JsonNode`, `JsonObject`, `JsonArray`, or `JsonElement` for request inspection or business logic.
- Serialization and deserialization with `System.Text.Json` into and out of named DTO types is acceptable and expected.
- Prefer existing Xeno.Client service, websocket, logging, and dashboard patterns over new infrastructure.
- Keep prompts and user inputs out of logs unless an existing explicit diagnostic setting permits them.

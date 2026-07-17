# Local Inference Server Plan

## Status

Goal: add an optional Xeno.Client background service that exposes localhost-only OpenAI-compatible and Ollama-compatible HTTP APIs, proxies requests through the existing Xeno websocket tunnel, and adds matching dashboard, documentation, test, and Postman coverage.

Current state:

- [ ] Not started
- [ ] In progress
- [ ] Complete

## Product Decisions

- [ ] The feature is disabled by default.
- [ ] The listener is localhost-only. It is configurable in `~/.xeno/client.json`, but it must reject non-loopback hosts.
- [ ] The listener starts automatically when enabled and must not depend on the local dashboard being enabled.
- [ ] The service uses the configured client identity from `client.json`, including `ClientId`, `TenantId`, and related existing identity fields.
- [ ] The service returns 502-compatible provider-shaped errors when the websocket is disconnected, severed, or unauthorized.
- [ ] Streaming is supported for compatible OpenAI and Ollama generation/chat endpoints.
- [ ] Request bodies are amended, where possible, to request telemetry, token counts, timings, and statistics from the Xeno server response.
- [ ] Model listing returns all models returned by the Xeno server.
- [ ] Ollama `/api/ps` returns all active/running models known to the Xeno server, behaving as a shared large Ollama server.
- [ ] Unsupported endpoints return response bodies and HTTP status codes matching the detected provider family as closely as possible.
- [ ] CORS is supported for localhost app integration.
- [ ] The dashboard API Explorer provides executable sample requests and always targets the local inference server.
- [ ] Postman adds `Xeno Client > Local Ollama APIs` and `Xeno Client > Local OpenAI APIs`.

## Compatibility Scope

### OpenAI-Compatible APIs

- [ ] `POST /v1/chat/completions`
  - [ ] Accept OpenAI-style chat completion requests with named DTOs.
  - [ ] Accept a single chat request per HTTP call.
  - [ ] Support non-streaming JSON responses.
  - [ ] Support streaming `text/event-stream` responses with OpenAI-style `chat.completion.chunk` payloads and terminal `data: [DONE]`.
  - [ ] Include provider-compatible usage fields when available.
  - [ ] Return OpenAI-shaped errors for validation failures, upstream errors, websocket failures, and unsupported options.
- [ ] `POST /v1/embeddings`
  - [ ] Accept `input` as a single string or an array of strings.
  - [ ] Return OpenAI-style embedding list responses with deterministic `index` values.
  - [ ] Include usage fields when available.
  - [ ] Return OpenAI-shaped errors for invalid input and upstream failures.
- [ ] `GET /v1/models`
  - [ ] Return all Xeno server-visible models in OpenAI `list` format.
  - [ ] Preserve enough metadata for client compatibility without exposing Xeno-only internals in unexpected fields.
- [ ] `GET /v1/models/{model}`
  - [ ] Return one OpenAI-style model object when the model is known.
  - [ ] Return OpenAI-style 404 errors when the model is unknown.
- [ ] Unsupported `/v1/*` endpoints
  - [ ] Return OpenAI-shaped error responses with appropriate status codes.
  - [ ] Do not expose stack traces or internal transport details.

### Ollama-Compatible APIs

- [ ] `POST /api/generate`
  - [ ] Accept Ollama text generation requests with named DTOs.
  - [ ] Accept a single generation request per HTTP call.
  - [ ] Support Ollama default streaming newline-delimited JSON.
  - [ ] Support `stream: false` single JSON response.
  - [ ] Include Ollama-compatible timing, token, and count fields when available.
  - [ ] Return Ollama-shaped errors for validation failures, upstream errors, websocket failures, and unsupported options.
- [ ] `POST /api/chat`
  - [ ] Accept Ollama chat requests with named DTOs.
  - [ ] Accept a single chat request per HTTP call.
  - [ ] Support Ollama default streaming newline-delimited JSON.
  - [ ] Support `stream: false` single JSON response.
  - [ ] Include Ollama-compatible timing, token, and count fields when available.
- [ ] `POST /api/embeddings`
  - [ ] Support legacy Ollama embeddings shape with `prompt`.
  - [ ] Return the legacy single `embedding` response shape.
  - [ ] Keep this DTO separate from the current `/api/embed` DTO.
- [ ] `POST /api/embed`
  - [ ] Support current Ollama embedding shape with `input` as a string or string array.
  - [ ] Return the current `embeddings` response shape.
  - [ ] Include timing fields when available.
- [ ] `GET /api/tags`
  - [ ] Return all Xeno server-visible models in Ollama tag format.
- [ ] `POST /api/show`
  - [ ] Return Ollama-compatible model detail responses.
  - [ ] Return Ollama-shaped 404 errors for unknown models.
- [ ] `GET /api/ps`
  - [ ] Return all active/running models known to the Xeno server.
  - [ ] Treat the Xeno server as a shared Ollama runtime rather than only this local process.
- [ ] Unsupported `/api/*` endpoints
  - [ ] Return Ollama-shaped error responses with appropriate status codes.
  - [ ] Do not expose stack traces or internal transport details.

### Explicit Non-Goals

- [ ] Do not implement OpenAI JSONL batch APIs.
- [ ] Do not implement vLLM-specific APIs.
- [ ] Do not add authentication to the local listener.
- [ ] Do not bind to public interfaces.
- [ ] Do not require the local dashboard to be enabled.

## Configuration

- [ ] Add a new client settings section, likely `LocalInferenceServer`.
- [ ] Add a dedicated settings model in Xeno.Client, for example `ClientLocalInferenceServerSettings`.
- [ ] Default values:
  - [ ] `Enabled`: `false`
  - [ ] `Host`: `127.0.0.1`
  - [ ] `Port`: `11434`
  - [ ] `EnableCors`: `true`
  - [ ] `CorsAllowedOrigins`: localhost-only defaults, or wildcard only when safely limited to loopback binding
  - [ ] Optional request/response limits consistent with existing Xeno.Client conventions
- [ ] Update `ClientSettings.Load()` so loading an existing `client.json` backfills missing defaults and rewrites the file.
- [ ] Preserve existing user-provided settings and formatting behavior as much as the current serializer allows.
- [ ] Validate that the configured host is loopback only:
  - [ ] Accept `localhost`.
  - [ ] Accept `127.0.0.1`.
  - [ ] Accept `::1` and `[::1]`.
  - [ ] Reject `0.0.0.0`, public IPs, wildcard hosts, and DNS names that are not explicitly localhost.
- [ ] Add settings display and edit support wherever current client settings are shown or changed.
- [ ] Document the new `client.json` section and the default rewrite behavior.

## Architecture

- [ ] Add a standalone Xeno.Client service namespace, for example `Xeno.Client.LocalInference`.
- [ ] Build the service with Watson7 using the same project patterns as the local dashboard service.
- [ ] Run this as a separate background service from the local dashboard.
- [ ] Add a runtime manager, for example `LocalInferenceServerRuntime`, parallel to `LocalDashboardRuntime`.
- [ ] Start the runtime during client startup when `LocalInferenceServer.Enabled` is true.
- [ ] Stop and dispose the runtime cleanly during client shutdown.
- [ ] Log local inference server startup attempts through existing Xeno.Client logging patterns.
- [ ] Log startup failures with actionable details, including configured host, configured port, exception type, and likely remediation.
- [ ] Detect and clearly log port-level bind conflicts so the user can change `LocalInferenceServer.Port`.
- [ ] Keep the Xeno.Client process running if the local inference listener fails to start.
- [ ] Add client connection state fields for local inference status:
  - [ ] Enabled
  - [ ] Listening
  - [ ] URL
  - [ ] Last error
  - [ ] Startup timestamp
  - [ ] Last request timestamp
- [ ] Ensure the service can report healthy listener state even when the websocket is currently disconnected.
- [ ] Ensure request handling fails per-provider with 502 when the websocket cannot service the request.

## Watson7 Service Design

- [ ] Create the Watson webserver with configured loopback host and port.
- [ ] Register explicit routes for each supported OpenAI and Ollama endpoint.
- [ ] Register preflight handling for CORS.
- [ ] Add Swagger/OpenAPI metadata for every supported route.
- [ ] Expose the OpenAPI document and Swagger UI only on the local listener.
- [ ] Keep route handlers thin and delegate provider behavior to focused services.
- [ ] Implement request size, method, and content-type validation with provider-compatible errors.
- [ ] Tolerate common client headers such as `Authorization`, `OpenAI-Organization`, and Ollama client headers without requiring auth.
- [ ] Return provider-compatible 404s for unsupported routes, not Watson default HTML responses.
- [ ] Use cancellation tokens throughout request handling.
- [ ] Use `ConfigureAwait(false)` in library/server async code.

## DTO and Serialization Requirements

- [ ] Use named DTO classes for all request and response payloads.
- [ ] Do not inspect application payloads through `JsonNode`, `JsonObject`, `JsonArray`, or `JsonElement`.
- [ ] Avoid string-contains checks against JSON.
- [ ] Add custom converters only where protocol compatibility requires union shapes, such as:
  - [ ] OpenAI embeddings `input` string or string array.
  - [ ] Ollama `/api/embed` `input` string or string array.
  - [ ] Provider options dictionaries if unavoidable.
- [ ] Prefer strongly named option DTOs for known provider options.
- [ ] Keep DTOs one class per file where practical, following repository style.
- [ ] Use explicit property names and `JsonPropertyName` attributes where wire casing differs from C# names.
- [ ] Add XML documentation for public DTOs and public service APIs.
- [ ] Ensure serialization omits nulls only where the provider API expects omission.

## Websocket Proxy Contract

- [ ] Identify the existing websocket request path used by Xeno.Client to ask the Xeno server for model, embedding, chat, and completion work.
- [ ] Add or reuse typed client-to-server messages for:
  - [ ] Model list
  - [ ] Model details
  - [ ] Running/active model list
  - [ ] Text generation
  - [ ] Chat completion
  - [ ] Embeddings
- [ ] Include the configured client identity and tenant context in proxied requests.
- [ ] Include telemetry/statistics request flags when the upstream contract supports them.
- [ ] Add streaming response support through the websocket tunnel.
- [ ] Preserve request cancellation from the local HTTP client through websocket cancellation where supported.
- [ ] Map upstream provider/model errors into OpenAI or Ollama response shapes.
- [ ] Map transport loss, authorization failure, or disconnected state into provider-shaped 502 responses.
- [ ] Propagate Xeno Server HTTP response headers that are returned through the websocket onto localhost responses from the local inference server.
- [ ] Exclude hop-by-hop, invalid, duplicate-unsafe, and listener-owned headers from propagation, including `Connection`, `Transfer-Encoding`, `Keep-Alive`, `Upgrade`, `Proxy-Authenticate`, `Proxy-Authorization`, `TE`, `Trailer`, and local `Content-Length`.
- [ ] Preserve provider-relevant headers such as request IDs, rate-limit metadata, model/provider metadata, cache metadata, and server timing when supplied by Xeno Server.
- [ ] Define deterministic behavior when propagated headers conflict with required OpenAI/Ollama compatibility headers.
- [ ] Ensure concurrent HTTP requests can safely share the websocket tunnel.
- [ ] Add backpressure or bounded buffering for streaming responses.

## Streaming Behavior

- [ ] OpenAI chat streaming:
  - [ ] Use `Content-Type: text/event-stream`.
  - [ ] Emit `data: {json}` events for chunks.
  - [ ] Emit `data: [DONE]` when complete.
  - [ ] Flush after chunks where Watson7 supports it.
  - [ ] Map upstream stream errors to an OpenAI-style SSE error event when possible, otherwise terminate cleanly.
- [ ] Ollama generate/chat streaming:
  - [ ] Use newline-delimited JSON.
  - [ ] Match Ollama `done: false` chunk behavior.
  - [ ] Emit final `done: true` object with timing and count fields when available.
  - [ ] Support `stream: false` aggregation into a single final response.
- [ ] Embeddings and model APIs remain non-streaming.
- [ ] Add tests that validate streaming framing, termination, and error mapping.

## Error Mapping

- [ ] Implement provider-specific error factories.
- [ ] OpenAI error shape:
  - [ ] Include `error.message`.
  - [ ] Include `error.type`.
  - [ ] Include `error.param` where applicable.
  - [ ] Include `error.code` where applicable.
  - [ ] Use OpenAI-compatible statuses for 400, 404, 405, 413, 415, 422, 429, 500, and 502.
- [ ] Ollama error shape:
  - [ ] Include `error`.
  - [ ] Use Ollama-compatible statuses for 400, 404, 405, 413, 415, 500, and 502.
- [ ] Detect provider family by route prefix:
  - [ ] `/v1/*` uses OpenAI-compatible errors.
  - [ ] `/api/*` uses Ollama-compatible errors.
  - [ ] Unknown prefixes use a small JSON error without internal details.
- [ ] Log local errors through existing Xeno.Client logging patterns.
- [ ] Do not log prompt or embedding input content unless an existing opt-in diagnostic setting explicitly allows it.

## Dashboard API Explorer

- [ ] Add a local dashboard page for the local inference API explorer.
- [ ] Keep visual structure and behavior aligned with the server dashboard API Explorer.
- [ ] Show only Local OpenAI APIs and Local Ollama APIs.
- [ ] Always target the local inference server base URL.
- [ ] Show enabled/listening/error status from local client state.
- [ ] Provide executable examples for:
  - [ ] OpenAI chat completions
  - [ ] OpenAI streaming chat completions
  - [ ] OpenAI embeddings
  - [ ] OpenAI model list
  - [ ] OpenAI model details
  - [ ] Ollama generate
  - [ ] Ollama streaming generate
  - [ ] Ollama chat
  - [ ] Ollama streaming chat
  - [ ] Ollama legacy embeddings
  - [ ] Ollama current embed
  - [ ] Ollama tags
  - [ ] Ollama show
  - [ ] Ollama ps
- [ ] Include copyable curl requests.
- [ ] Include editable request bodies where the server dashboard API Explorer pattern supports it.
- [ ] Execute requests from the browser and render provider-shaped responses.
- [ ] Handle disabled listener and websocket unavailable cases clearly.
- [ ] Add settings visibility for the local inference server configuration.
- [ ] Update static fallback dashboard assets if the project still relies on embedded fallback HTML/JS.

## CLI and Status Output

- [ ] Include local inference server details in `xeno status`.
- [ ] Include local inference server details in any related client status, diagnostics, or settings commands where local dashboard details are already shown.
- [ ] Show whether the local inference server is enabled.
- [ ] Show whether the local inference server is listening.
- [ ] Show the configured local inference server URL when enabled.
- [ ] Show startup or listener errors when present, including port conflict details.
- [ ] Show websocket availability impact clearly, including that local HTTP endpoints will return provider-shaped 502 responses when the tunnel is unavailable.
- [ ] Keep disabled output concise so default installations are not noisy.
- [ ] Add tests or command-output snapshots for updated CLI/status output where the repository has coverage for command behavior.

## Postman

- [ ] Update `Xeno.postman_collection.json`.
- [ ] Add collection variable `localInferenceBaseUrl`, defaulting to `http://127.0.0.1:11434`.
- [ ] Under `Xeno Client`, add folder `Local OpenAI APIs`.
- [ ] Under `Xeno Client`, add folder `Local Ollama APIs`.
- [ ] Add executable OpenAI requests:
  - [ ] Chat completions
  - [ ] Streaming chat completions where Postman behavior supports it
  - [ ] Embeddings with single input
  - [ ] Embeddings with batch input
  - [ ] List models
  - [ ] Retrieve model
- [ ] Add executable Ollama requests:
  - [ ] Generate
  - [ ] Generate streaming where Postman behavior supports it
  - [ ] Chat
  - [ ] Chat streaming where Postman behavior supports it
  - [ ] Legacy embeddings
  - [ ] Embed single input
  - [ ] Embed batch input
  - [ ] Tags
  - [ ] Show
  - [ ] Ps
- [ ] Add brief request descriptions explaining local listener requirements.

## Documentation

- [ ] Update markdown files that introduce or operate Xeno.Client:
  - [ ] `CLIENT_GETTING_STARTED.md`
  - [ ] `README.md`
  - [ ] `getting-started/WINDOWS_CLIENT.md`
  - [ ] `getting-started/MACOS_CLIENT.md`
  - [ ] `getting-started/LINUX_CLIENT.md`
- [ ] Update user-facing client documentation to describe:
  - [ ] Purpose of the local inference server.
  - [ ] How to enable it in `~/.xeno/client.json`.
  - [ ] Default host and port.
  - [ ] Loopback-only binding behavior.
  - [ ] No local auth requirement.
  - [ ] Websocket dependency and 502 behavior.
  - [ ] OpenAI-compatible base URL.
  - [ ] Ollama-compatible base URL.
  - [ ] Streaming support.
  - [ ] Supported and unsupported endpoints.
- [ ] Update developer documentation for:
  - [ ] Service architecture.
  - [ ] DTO conventions.
  - [ ] Websocket proxy message contracts.
  - [ ] Testing strategy.
- [ ] Update release notes or changelog if the repository uses one.

## Tests

- [ ] Add unit tests for settings load/backfill/save behavior.
- [ ] Add unit tests for loopback host validation.
- [ ] Add unit tests for local inference startup failure logging.
- [ ] Add unit tests for port conflict handling and connection state updates.
- [ ] Add unit tests for route matching and unsupported endpoint provider detection.
- [ ] Add unit tests for OpenAI request validation.
- [ ] Add unit tests for Ollama request validation.
- [ ] Add unit tests for OpenAI error response mapping.
- [ ] Add unit tests for Ollama error response mapping.
- [ ] Add unit tests for Xeno Server response header propagation and excluded hop-by-hop headers.
- [ ] Add unit tests for websocket disconnected and unauthorized 502 mapping.
- [ ] Add unit tests for OpenAI embeddings single and batch inputs.
- [ ] Add unit tests for Ollama `/api/embeddings` legacy input.
- [ ] Add unit tests for Ollama `/api/embed` single and batch inputs.
- [ ] Add integration tests using an in-process or loopback local inference server with a fake typed websocket proxy.
- [ ] Add integration tests for OpenAI non-streaming chat completions.
- [ ] Add integration tests for OpenAI streaming chat completions.
- [ ] Add integration tests for Ollama non-streaming generate.
- [ ] Add integration tests for Ollama streaming generate.
- [ ] Add integration tests for Ollama non-streaming chat.
- [ ] Add integration tests for Ollama streaming chat.
- [ ] Add integration tests for model list, model details, tags, show, and ps.
- [ ] Add CORS preflight tests.
- [ ] Add concurrency tests for multiple simultaneous local HTTP requests.
- [ ] Add cancellation tests for a client-disconnected streaming request.
- [ ] Add shared Touchstone descriptors where the behavior is product-facing and should run across automated, Xunit, and NUnit harnesses.
- [ ] Add dashboard tests for the API Explorer page if the repository has a dashboard test harness.
- [ ] Add Postman collection structural validation if an existing collection validation step exists.

## Validation Commands

Run these before marking the implementation complete:

- [ ] `dotnet build src/Xeno.sln`
- [ ] `dotnet run --project src/Test.Automated -- --results results.json`
- [ ] `dotnet test src/Test.Xunit`
- [ ] `dotnet test src/Test.Nunit`
- [ ] Dashboard build/test command used by this repository, if present.
- [ ] Manual smoke test with `LocalInferenceServer.Enabled=true`.
- [ ] Manual OpenAI curl smoke test against `http://127.0.0.1:11434/v1/models`.
- [ ] Manual Ollama curl smoke test against `http://127.0.0.1:11434/api/tags`.
- [ ] Manual websocket-disconnected smoke test confirming provider-shaped 502 responses.

## Implementation Order

- [ ] Phase 1: Configuration and lifecycle
  - [ ] Add settings DTO and defaults.
  - [ ] Add load/backfill/save support.
  - [ ] Add lifecycle runtime and connection state.
  - [ ] Add loopback host validation.
- [ ] Phase 2: DTOs and provider protocol layer
  - [ ] Add OpenAI DTOs.
  - [ ] Add Ollama DTOs.
  - [ ] Add provider-specific error factories.
  - [ ] Add input union converters without raw JSON inspection.
- [ ] Phase 3: Watson7 listener
  - [ ] Add service class and route registration.
  - [ ] Add CORS and preflight support.
  - [ ] Add OpenAPI/Swagger metadata.
  - [ ] Add unsupported route handling.
- [ ] Phase 4: Websocket proxy
  - [ ] Reuse or add typed websocket messages.
  - [ ] Add model, running-model, generation, chat, and embedding proxy operations.
  - [ ] Add streaming bridge and cancellation handling.
  - [ ] Add telemetry/statistics request flags.
- [ ] Phase 5: Dashboard
  - [ ] Add local API Explorer page.
  - [ ] Add executable samples and response rendering.
  - [ ] Add status/settings visibility.
  - [ ] Update fallback static dashboard path if required.
- [ ] Phase 6: Postman and docs
  - [ ] Add Postman folders and requests.
  - [ ] Update user docs.
  - [ ] Update developer docs and release notes.
- [ ] Phase 7: Tests and validation
  - [ ] Add unit tests.
  - [ ] Add integration tests.
  - [ ] Add Touchstone descriptors where appropriate.
  - [ ] Run the full validation command set.
  - [ ] Fix all compiler warnings and test failures.
- [ ] Phase 8: Repository agent guidance
  - [ ] Edit `AGENTS.md` as the final implementation step.
  - [ ] Make `AGENTS.md` complete for the new local inference server architecture, configuration, testing, documentation, and dashboard expectations.
  - [ ] Verify the final `AGENTS.md` guidance matches the implemented behavior and does not contradict `C:\code\agents\requirements`.

## Acceptance Criteria

- [ ] A fresh `client.json` includes `LocalInferenceServer` defaults with the feature disabled.
- [ ] An existing `client.json` is automatically rewritten after load to include missing defaults.
- [ ] Enabling the feature starts a Watson7 local listener independently of the dashboard.
- [ ] The listener rejects non-loopback host configuration.
- [ ] OpenAI-compatible clients can list models, retrieve a model, request embeddings, and request chat completions.
- [ ] Ollama-compatible clients can list tags, show a model, list running models, request embeddings, generate text, and chat.
- [ ] Streaming works for OpenAI chat completions and Ollama generate/chat.
- [ ] When the websocket is unavailable, supported endpoints return provider-shaped 502 responses.
- [ ] Unsupported OpenAI and Ollama paths return provider-shaped errors.
- [ ] No API handler uses raw JSON DOM peeking for normal request processing.
- [ ] The local dashboard exposes an executable API Explorer for only these local OpenAI and Ollama APIs.
- [ ] Postman includes executable Local OpenAI and Local Ollama folders under Xeno Client.
- [ ] Documentation explains configuration, supported APIs, streaming, and failure behavior.
- [ ] Full repository validation passes without compiler warnings.

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

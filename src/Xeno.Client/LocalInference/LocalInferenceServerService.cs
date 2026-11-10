namespace Xeno.Client.LocalInference
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using Xeno.Client.LocalInference.Models;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Watson-backed localhost OpenAI-compatible and Ollama-compatible inference listener.
    /// </summary>
    public sealed class LocalInferenceServerService : IDisposable
    {
        private readonly ClientSettings _Settings;
        private readonly Webserver _Webserver;
        private const string RouteHeaderName = "X-Xeno-Route";
        private const string FallbackHeaderName = "X-Xeno-Fallback";
        private static readonly string[] _RequestHeaderResponseDenyList =
        {
            "Accept",
            "Accept-Charset",
            "Accept-Encoding",
            "Accept-Language",
            "Cache-Control",
            "Connection",
            "Content-Length",
            "Expect",
            "Host",
            "User-Agent"
        };
        private readonly LocalBackendHealthMonitor _Monitor;
        private readonly LocalProviderProxy _Proxy;
        private readonly object _SettingsLock = new object();
        private ClientSettings _Current;
        private DateTime? _SettingsWriteUtc;
        private readonly DateTime _StartedUtc = DateTime.UtcNow;
        private readonly CancellationTokenSource _StatusCancellation = new CancellationTokenSource();
        private Task? _StatusTask;
        private long _LocalRequests;
        private long _NetworkRequests;
        private long _Fallbacks;
        private long _Errors;
        private int _StatusDirty = 1;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the local inference server.
        /// </summary>
        /// <param name="settings">Client settings. The listener host and port come from these settings for the listener's lifetime; routing, rules, and providers follow later saves of client.json.</param>
        /// <param name="providerHandler">Optional HTTP handler for provider calls and health checks, for tests.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the listener host is not loopback.</exception>
        public LocalInferenceServerService(ClientSettings settings, System.Net.Http.HttpMessageHandler? providerHandler = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Current = settings;
            _SettingsWriteUtc = SettingsFileWriteUtc();
            _Monitor = new LocalBackendHealthMonitor(CurrentSettings, providerHandler);
            _Proxy = new LocalProviderProxy(providerHandler);
            ValidateLoopbackHost(_Settings.LocalInferenceServer.Host);
            WebserverSettings webserverSettings = new WebserverSettings(_Settings.LocalInferenceServer.Host, _Settings.LocalInferenceServer.Port, false);
            _Webserver = new Webserver(webserverSettings, DefaultRouteAsync);
            _Webserver.Routes.Preflight = PreflightRouteAsync;
            _Webserver.Routes.PostRouting = PostRoutingRouteAsync;
        }

        /// <summary>
        /// Listener URL.
        /// </summary>
        public string Url
        {
            get
            {
                string host = String.Equals(_Settings.LocalInferenceServer.Host, "[::1]", StringComparison.OrdinalIgnoreCase) ? "[::1]" : _Settings.LocalInferenceServer.Host;
                return "http://" + host + ":" + _Settings.LocalInferenceServer.Port.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Provider health checker used by the router. Exposed so status surfaces and tests can read the same snapshots.
        /// </summary>
        public LocalBackendHealthMonitor Backends
        {
            get
            {
                return _Monitor;
            }
        }

        /// <summary>
        /// Start the listener and the provider health checks.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _Webserver.Start();
            if (CurrentSettings().LocalInferenceServer.RoutingMode != LocalInferenceRoutingModeEnum.NetworkOnly) _Monitor.Start();
            CancellationToken statusToken = _StatusCancellation.Token;
            _StatusTask = Task.Run(async () => await WriteStatusLoopAsync(statusToken).ConfigureAwait(false), statusToken);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the listener.
        /// </summary>
        public void Stop()
        {
            try
            {
                _Webserver.Stop();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already stopped", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Validate that a host is loopback-only.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <exception cref="ArgumentException">Thrown when the host is not loopback-only.</exception>
        public static void ValidateLoopbackHost(string host)
        {
            LocalSettingsEditor.ValidateLocalInferenceHost(host);
        }

        private async Task PreflightRouteAsync(HttpContextBase context)
        {
            ClearResponseHeaders(context);
            ApplyCorsHeaders(context);
            context.Response.StatusCode = 204;
            SanitizeResponseHeaders(context);
            await context.Response.Send(context.Token).ConfigureAwait(false);
        }

        private Task PostRoutingRouteAsync(HttpContextBase context)
        {
            ApplyCorsHeaders(context);
            SanitizeResponseHeaders(context);
            return Task.CompletedTask;
        }

        private async Task DefaultRouteAsync(HttpContextBase context)
        {
            string path = context.Request.Url.RawWithoutQuery ?? "/";
            string method = context.Request.Method.ToString().ToUpperInvariant();
            ClientConnectionState.MarkLocalInferenceRequest(_Settings);
            try
            {
                ClearResponseHeaders(context);
                ApplyCorsHeaders(context);
                if ((method == "HEAD" || method == "GET") && String.Equals(path, "/", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 200;
                    SanitizeResponseHeaders(context);
                    await context.Response.Send(context.Token).ConfigureAwait(false);
                    return;
                }

                if (method == "HEAD" && IsProviderPath(path))
                {
                    context.Response.StatusCode = 200;
                    SanitizeResponseHeaders(context);
                    await context.Response.Send(context.Token).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && String.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
                {
                    await SendJsonAsync(context, new LocalInferenceGenericErrorResponse { Error = "Healthy", Message = "Xeno local inference server is healthy." }).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && String.Equals(path, "/openapi.json", StringComparison.OrdinalIgnoreCase))
                {
                    await SendJsonAsync(context, BuildOpenApiDocument()).ConfigureAwait(false);
                    return;
                }

                if (method == "GET" && String.Equals(path, "/swagger", StringComparison.OrdinalIgnoreCase))
                {
                    await SendHtmlAsync(context, SwaggerHtml()).ConfigureAwait(false);
                    return;
                }

                LocalInferenceRequestRecord record = new LocalInferenceRequestRecord { Id = Guid.NewGuid().ToString("N"), Method = method, Path = path };
                context.Metadata = record;
                System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    await HandleProviderRouteAsync(context, method, path, context.Token).ConfigureAwait(false);
                }
                finally
                {
                    record.LatencyMs = clock.Elapsed.TotalMilliseconds;
                }
            }
            catch (InvalidDataException ex)
            {
                await SendProviderErrorAsync(context, path, 400, ex.Message, "invalid_request_error", "invalid_request").ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await SendProviderErrorAsync(context, path, 400, ex.Message, "invalid_request_error", "invalid_request").ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.Token.IsCancellationRequested)
            {
                await SendProviderErrorAsync(context, path, 499, "The localhost caller disconnected.", "request_cancelled", "request_cancelled").ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is SocketException)
            {
                SetRouteHeaders(context, "network", false);
                await SendProviderErrorAsync(context, path, 502, "Xeno websocket tunnel is unavailable: " + ex.Message, "server_error", "websocket_unavailable").ConfigureAwait(false);
            }
            finally
            {
                CompleteRecord(context);
            }
        }

        private void CompleteRecord(HttpContextBase context)
        {
            LocalInferenceRequestRecord? record = context.Metadata as LocalInferenceRequestRecord;
            if (record == null) return;
            context.Metadata = null;
            record.StatusCode = context.Response.StatusCode;
            if (String.IsNullOrWhiteSpace(record.Route)) record.Route = context.Response.Headers.Get(RouteHeaderName) ?? String.Empty;
            if (!record.Fallback) record.Fallback = String.Equals(context.Response.Headers.Get(FallbackHeaderName), "true", StringComparison.OrdinalIgnoreCase);
            if (record.Route.StartsWith("local:", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref _LocalRequests);
            else if (String.Equals(record.Route, "network", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref _NetworkRequests);
            if (record.Fallback) Interlocked.Increment(ref _Fallbacks);
            if (record.StatusCode >= 400) Interlocked.Increment(ref _Errors);
            Interlocked.Exchange(ref _StatusDirty, 1);
            int maxEntries = CurrentSettings().LocalDashboard?.HistoryRetention?.MaxEntries ?? 1000;
            LocalInferenceRequestLog.Append(record, maxEntries);
        }

        private static LocalInferenceRequestRecord? Record(HttpContextBase context)
        {
            return context.Metadata as LocalInferenceRequestRecord;
        }

        private async Task WriteStatusLoopAsync(CancellationToken token)
        {
            int ticks = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                ticks++;
                if (Interlocked.Exchange(ref _StatusDirty, 0) == 0 && ticks < 5) continue;
                ticks = 0;
                WriteStatus();
            }
        }

        private void WriteStatus()
        {
            ClientSettings settings = CurrentSettings();
            new LocalInferenceRuntimeStatus
            {
                UpdatedUtc = DateTime.UtcNow,
                StartedUtc = _StartedUtc,
                Backends = new List<LocalBackendStatus>(_Monitor.Statuses),
                Concurrency = ProviderConcurrencyGate.Shared.Snapshot(settings),
                LocalRequests = Interlocked.Read(ref _LocalRequests),
                NetworkRequests = Interlocked.Read(ref _NetworkRequests),
                Fallbacks = Interlocked.Read(ref _Fallbacks),
                Errors = Interlocked.Read(ref _Errors)
            }.Save();
        }

        private async Task HandleProviderRouteAsync(HttpContextBase context, string method, string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (method == "GET" && String.Equals(path, "/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                await SendOpenAiModelListAsync(context, path, method, token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path.StartsWith("/v1/models/", StringComparison.OrdinalIgnoreCase))
            {
                string model = Uri.UnescapeDataString(path.Substring("/v1/models/".Length));
                await SendOpenAiModelAsync(context, path, method, model, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OpenAiChatCompletionRequest request = RequireJson<OpenAiChatCompletionRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("OpenAI chat completion request requires model.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.OpenAi, request.Model, false, request.Stream.GetValueOrDefault(), body, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/v1/completions", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OpenAiCompletionRequest request = RequireJson<OpenAiCompletionRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("OpenAI completion request requires model.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.OpenAi, request.Model, false, request.Stream.GetValueOrDefault(), body, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/v1/embeddings", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OpenAiEmbeddingRequest request = RequireJson<OpenAiEmbeddingRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("OpenAI embeddings request requires model.");
                if (request.Input == null || request.Input.Values.Count == 0) throw new ArgumentException("OpenAI embeddings request requires input.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.OpenAi, request.Model, true, false, body, token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/tags", StringComparison.OrdinalIgnoreCase))
            {
                await SendOllamaTagsAsync(context, path, method, token).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/ps", StringComparison.OrdinalIgnoreCase))
            {
                await SendOllamaPsAsync(context, path, method, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/show", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaShowRequest request = RequireJson<OllamaShowRequest>(body);
                string? model = String.IsNullOrWhiteSpace(request.Model) ? request.Name : request.Model;
                if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("Ollama show request requires model.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.Ollama, model, false, false, body, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaGenerateRequest request = RequireJson<OllamaGenerateRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama generate request requires model.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.Ollama, request.Model, false, request.Stream != false, body, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/chat", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaChatRequest request = RequireJson<OllamaChatRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama chat request requires model.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.Ollama, request.Model, false, request.Stream != false, body, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/embeddings", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaLegacyEmbeddingRequest request = RequireJson<OllamaLegacyEmbeddingRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama embeddings request requires model.");
                if (String.IsNullOrWhiteSpace(request.Prompt)) throw new ArgumentException("Ollama embeddings request requires prompt.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.Ollama, request.Model, true, false, body, token).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/embed", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaEmbedRequest request = RequireJson<OllamaEmbedRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama embed request requires model.");
                if (request.Input == null || request.Input.Values.Count == 0) throw new ArgumentException("Ollama embed request requires input.");
                await RouteAsync(context, path, method, LocalInferenceApiShapeEnum.Ollama, request.Model, true, false, body, token).ConfigureAwait(false);
                return;
            }

            await SendProviderErrorAsync(context, path, 404, "Route not found.", "invalid_request_error", "not_found").ConfigureAwait(false);
        }

        private async Task RouteAsync(HttpContextBase context, string path, string method, LocalInferenceApiShapeEnum shape, string model, bool embeddings, bool stream, string body, CancellationToken token)
        {
            ClientSettings settings = CurrentSettings();
            string family = shape == LocalInferenceApiShapeEnum.OpenAi ? "openai" : "ollama";
            LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, _Monitor.Statuses, model, shape, embeddings);
            LocalInferenceRequestRecord? record = Record(context);
            if (record != null)
            {
                record.Model = model;
                record.Stream = stream;
                record.Reason = decision.Reason;
                record.Provider = decision.ProviderName;
                record.Fallback = decision.IsFallback;
            }

            if (decision.Target == LocalInferenceRouteTargetKindEnum.Network)
            {
                await ProxyAsync(context, path, method, family, model, embeddings, stream, body, "network", decision.IsFallback, decision.IsFallback).ConfigureAwait(false);
                return;
            }

            if (decision.Target == LocalInferenceRouteTargetKindEnum.Unavailable || decision.Provider == null)
            {
                SetRouteHeaders(context, "unavailable", false);
                if (decision.Reason == "localOnlyMode")
                {
                    await SendProviderErrorAsync(context, path, 404, "Model '" + model + "' is not served on this machine, and local inference routing is set to LocalOnly.", "invalid_request_error", "model_not_found").ConfigureAwait(false);
                    return;
                }

                await SendProviderErrorAsync(context, path, 502, "The route rule for model '" + model + "' names a provider that is not configured for local requests on this machine.", "server_error", "route_provider_unavailable").ConfigureAwait(false);
                return;
            }

            ClientRunnerSettings provider = decision.Provider;
            if (!decision.ShapeSupported)
            {
                SetRouteHeaders(context, decision.RouteHeader, false);
                await SendProviderErrorAsync(context, path, 400, "Model '" + model + "' is served on this machine by provider '" + provider.Name + "' (" + provider.ApiType + "), which accepts OpenAI-compatible requests only. Call the /v1 routes for this model.", "invalid_request_error", "unsupported_api_shape").ConfigureAwait(false);
                return;
            }

            Console.WriteLine(
                "Routing local " + family + " inference request " + method + " " + path
                + " to provider " + provider.Name + " (model=" + model
                + ", stream=" + (stream ? "true" : "false")
                + ", embeddings=" + (embeddings ? "true" : "false")
                + ", reason=" + decision.Reason + ").");
            int timeoutMs = Math.Max(1000, settings.LocalInferenceServer.RequestTimeoutMs);
            ConcurrencyLease? lease = await ProviderConcurrencyGate.Shared.AcquireLocalAsync(settings, provider, model, settings.LocalInferenceServer.LocalQueueTimeoutMs, context.Token).ConfigureAwait(false);
            if (lease == null)
            {
                SetRouteHeaders(context, decision.RouteHeader, false);
                context.Response.Headers.Add("Retry-After", "1");
                if (record != null) record.Error = "Provider at its concurrency limit.";
                await SendProviderErrorAsync(context, path, 429, "Provider '" + provider.Name + "' is at its concurrency limit and no slot freed up within " + settings.LocalInferenceServer.LocalQueueTimeoutMs.ToString(CultureInfo.InvariantCulture) + " ms.", "rate_limit_error", "provider_busy").ConfigureAwait(false);
                return;
            }

            LocalProviderProxyResult result;
            using (lease)
            {
                if (record != null) record.QueuedMs = lease.WaitedMs;
                result = await _Proxy.ForwardAsync(context, provider, method, path, body, stream, timeoutMs, delegate (HttpContextBase response) { SetRouteHeaders(response, decision.RouteHeader, false); }, context.Token).ConfigureAwait(false);
            }

            if (record != null)
            {
                record.FirstByteMs = result.FirstByteMs;
                record.ResponseBytes = result.ResponseBytes;
                record.PromptTokens = result.PromptTokens;
                record.CompletionTokens = result.CompletionTokens;
                if (!String.IsNullOrWhiteSpace(result.Error)) record.Error = result.Error;
            }

            if (result.Started)
            {
                if (result.StatusCode.HasValue && result.StatusCode.Value < 500) _Monitor.MarkSuccess(provider);
                if (!result.Completed) _Monitor.MarkFailure(provider, result.Error);
                return;
            }

            _Monitor.MarkFailure(provider, result.Error);
            if (decision.FallbackToNetwork)
            {
                Console.WriteLine("Local provider " + provider.Name + " failed before responding; falling back to the Xeno network for model " + model + ".");
                if (record != null)
                {
                    record.Fallback = true;
                    record.Provider = String.Empty;
                }

                await ProxyAsync(context, path, method, family, model, embeddings, stream, body, "network", true, true).ConfigureAwait(false);
                return;
            }

            SetRouteHeaders(context, decision.RouteHeader, false);
            await SendProviderErrorAsync(context, path, 502, result.Error, "server_error", "local_provider_unavailable").ConfigureAwait(false);
        }

        private async Task SendOpenAiModelListAsync(HttpContextBase context, string path, string method, CancellationToken token)
        {
            ClientSettings settings = CurrentSettings();
            LocalInferenceRoutingModeEnum mode = settings.LocalInferenceServer.RoutingMode;
            if (mode == LocalInferenceRoutingModeEnum.NetworkOnly)
            {
                await ProxyAsync(context, path, method, "openai", null, false, false, "{}", "network", false, false).ConfigureAwait(false);
                return;
            }

            List<LocalOpenAiModel> local = LocalModelListingBuilder.LocalOpenAiModels(settings, _Monitor.Statuses);
            LocalOpenAiModelList? network = mode == LocalInferenceRoutingModeEnum.LocalOnly ? null : await TryTunnelJsonAsync<LocalOpenAiModelList>(context, path, method, "openai", token).ConfigureAwait(false);
            if (network == null && local.Count == 0 && mode != LocalInferenceRoutingModeEnum.LocalOnly)
            {
                SetRouteHeaders(context, "unavailable", false);
                await SendProviderErrorAsync(context, path, 502, "Neither this machine's providers nor the Xeno websocket tunnel returned models.", "server_error", "websocket_unavailable").ConfigureAwait(false);
                return;
            }

            SetRouteHeaders(context, ListingRoute(local.Count > 0, network != null), false);
            await SendJsonAsync(context, LocalModelListingBuilder.MergeOpenAi(local, network?.Data)).ConfigureAwait(false);
        }

        private async Task SendOpenAiModelAsync(HttpContextBase context, string path, string method, string model, CancellationToken token)
        {
            ClientSettings settings = CurrentSettings();
            LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, _Monitor.Statuses, model, LocalInferenceApiShapeEnum.OpenAi, false);
            if (decision.Target == LocalInferenceRouteTargetKindEnum.Local && decision.Provider != null)
            {
                SetRouteHeaders(context, decision.RouteHeader, false);
                await SendJsonAsync(context, new LocalOpenAiModel { Id = model, OwnedBy = decision.Provider.Name }).ConfigureAwait(false);
                return;
            }

            if (decision.Target == LocalInferenceRouteTargetKindEnum.Network)
            {
                await ProxyAsync(context, path, method, "openai", model, false, false, "{}", "network", false, false).ConfigureAwait(false);
                return;
            }

            SetRouteHeaders(context, "unavailable", false);
            await SendProviderErrorAsync(context, path, 404, "Model not found: " + model, "invalid_request_error", "model_not_found").ConfigureAwait(false);
        }

        private async Task SendOllamaTagsAsync(HttpContextBase context, string path, string method, CancellationToken token)
        {
            ClientSettings settings = CurrentSettings();
            LocalInferenceRoutingModeEnum mode = settings.LocalInferenceServer.RoutingMode;
            if (mode == LocalInferenceRoutingModeEnum.NetworkOnly)
            {
                await ProxyAsync(context, path, method, "ollama", null, false, false, "{}", "network", false, false).ConfigureAwait(false);
                return;
            }

            List<List<LocalOllamaModel>> local = new List<List<LocalOllamaModel>>();
            foreach (ClientRunnerSettings provider in LocalOllamaProviders(settings))
            {
                LocalOllamaTagsResponse? tags = await _Proxy.GetJsonAsync<LocalOllamaTagsResponse>(provider, "/api/tags", ListingTimeoutMs(provider), token).ConfigureAwait(false);
                if (tags == null)
                {
                    _Monitor.MarkFailure(provider, "Provider '" + provider.Name + "' did not return its model list.");
                    continue;
                }

                local.Add(LocalModelListingBuilder.RoutableOllamaModels(settings, provider, tags));
            }

            LocalOllamaTagsResponse? network = mode == LocalInferenceRoutingModeEnum.LocalOnly ? null : await TryTunnelJsonAsync<LocalOllamaTagsResponse>(context, path, method, "ollama", token).ConfigureAwait(false);
            bool anyLocal = local.Count > 0;
            if (network == null && !anyLocal && mode != LocalInferenceRoutingModeEnum.LocalOnly)
            {
                SetRouteHeaders(context, "unavailable", false);
                await SendProviderErrorAsync(context, path, 502, "Neither this machine's providers nor the Xeno websocket tunnel returned models.", "server_error", "websocket_unavailable").ConfigureAwait(false);
                return;
            }

            List<IEnumerable<LocalOllamaModel>?> lists = new List<IEnumerable<LocalOllamaModel>?>(local);
            lists.Add(network?.Models);
            SetRouteHeaders(context, ListingRoute(anyLocal, network != null), false);
            await SendJsonAsync(context, LocalModelListingBuilder.MergeOllamaTags(lists)).ConfigureAwait(false);
        }

        private async Task SendOllamaPsAsync(HttpContextBase context, string path, string method, CancellationToken token)
        {
            ClientSettings settings = CurrentSettings();
            if (settings.LocalInferenceServer.RoutingMode == LocalInferenceRoutingModeEnum.NetworkOnly)
            {
                await ProxyAsync(context, path, method, "ollama", null, false, false, "{}", "network", false, false).ConfigureAwait(false);
                return;
            }

            List<IEnumerable<LocalOllamaRunningModel>?> lists = new List<IEnumerable<LocalOllamaRunningModel>?>();
            foreach (ClientRunnerSettings provider in LocalOllamaProviders(settings))
            {
                LocalOllamaPsResponse? ps = await _Proxy.GetJsonAsync<LocalOllamaPsResponse>(provider, "/api/ps", ListingTimeoutMs(provider), token).ConfigureAwait(false);
                if (ps != null) lists.Add(ps.Models);
            }

            SetRouteHeaders(context, "local", false);
            await SendJsonAsync(context, LocalModelListingBuilder.MergeOllamaPs(lists)).ConfigureAwait(false);
        }

        private async Task<T?> TryTunnelJsonAsync<T>(HttpContextBase context, string path, string method, string family, CancellationToken token) where T : class
        {
            ClientLocalInferenceRequestMessage request = BuildTunnelRequest(context, path, method, family, null, false, false, "{}", false);
            try
            {
                LocalInferencePendingResponse pending = await LocalInferenceTunnelBridge.SendRequestAsync(request, token).ConfigureAwait(false);
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, CurrentSettings().LocalInferenceServer.RequestTimeoutMs)));
                    using (timeout.Token.Register(delegate { LocalInferenceTunnelBridge.CancelPending(request.RequestId, timeout.Token); }))
                    {
                        ServerLocalInferenceResponseMessage first = await WaitForFirstResponseAsync(pending, timeout.Token).ConfigureAwait(false);
                        int status = first.StatusCode ?? (first.Success ? 200 : 502);
                        if (status < 200 || status >= 300 || String.IsNullOrWhiteSpace(first.Body)) return null;
                        return JsonUtil.Deserialize<T>(first.Body);
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is SocketException || ex is WebSocketException || ex is System.Text.Json.JsonException || (ex is OperationCanceledException && !token.IsCancellationRequested))
            {
                return null;
            }
        }

        private List<ClientRunnerSettings> LocalOllamaProviders(ClientSettings settings)
        {
            List<ClientRunnerSettings> providers = new List<ClientRunnerSettings>();
            foreach (ClientRunnerSettings provider in LocalInferenceRouter.RoutableProviders(settings))
            {
                if (!ProviderConfiguration.IsOllamaApiType(provider.ApiType)) continue;
                LocalBackendStatus? status = _Monitor.Get(provider.Name);
                if (status != null && status.Healthy == false) continue;
                providers.Add(provider);
            }

            return providers;
        }

        private static int ListingTimeoutMs(ClientRunnerSettings provider)
        {
            return Math.Min(5000, Math.Max(1000, provider.TimeoutMs));
        }

        private static string ListingRoute(bool local, bool network)
        {
            if (local && network) return "local+network";
            return local ? "local" : "network";
        }

        private static void SetRouteHeaders(HttpContextBase context, string route, bool fallback)
        {
            try
            {
                context.Response.Headers.Remove(RouteHeaderName);
                context.Response.Headers.Remove(FallbackHeaderName);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
            }

            context.Response.Headers.Add(RouteHeaderName, route);
            if (fallback) context.Response.Headers.Add(FallbackHeaderName, "true");
        }

        private ClientSettings CurrentSettings()
        {
            DateTime? written = SettingsFileWriteUtc();
            lock (_SettingsLock)
            {
                if (!written.HasValue || written == _SettingsWriteUtc) return _Current;
                try
                {
                    ClientSettings loaded = ClientSettings.Load();
                    loaded.LocalInferenceServer.Host = _Settings.LocalInferenceServer.Host;
                    loaded.LocalInferenceServer.Port = _Settings.LocalInferenceServer.Port;
                    _Current = loaded;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Text.Json.JsonException || ex is InvalidOperationException)
                {
                }

                _SettingsWriteUtc = written;
                return _Current;
            }
        }

        private static DateTime? SettingsFileWriteUtc()
        {
            try
            {
                return File.Exists(ClientSettings.SettingsFile) ? File.GetLastWriteTimeUtc(ClientSettings.SettingsFile) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsProviderPath(string path)
        {
            return String.Equals(path, "/v1/chat/completions", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/v1/completions", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/v1/embeddings", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/v1/models", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/v1/models/", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/generate", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/chat", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/embeddings", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/embed", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/tags", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/show", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(path, "/api/ps", StringComparison.OrdinalIgnoreCase);
        }

        private ClientLocalInferenceRequestMessage BuildTunnelRequest(HttpContextBase context, string path, string method, string providerFamily, string? model, bool embeddings, bool stream, string body, bool excludeRequestingClient)
        {
            return new ClientLocalInferenceRequestMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Method = method,
                RelativePath = path,
                ProviderFamily = providerFamily,
                Model = model,
                Embeddings = embeddings,
                Stream = stream,
                RequestorIp = context.Request.Source.IpAddress,
                Headers = CaptureRequestHeaders(context),
                Body = String.IsNullOrWhiteSpace(body) ? "{}" : body,
                ExcludeRequestingClient = excludeRequestingClient
            };
        }

        private async Task ProxyAsync(HttpContextBase context, string path, string method, string providerFamily, string? model, bool embeddings, bool stream, string body, string route, bool fallback, bool excludeRequestingClient)
        {
            int requestTimeoutMs = Math.Max(1000, CurrentSettings().LocalInferenceServer.RequestTimeoutMs);
            ClientLocalInferenceRequestMessage request = BuildTunnelRequest(context, path, method, providerFamily, model, embeddings, stream, body, excludeRequestingClient);
            SetRouteHeaders(context, route, fallback);

            Console.WriteLine(
                "Proxying local " + providerFamily + " inference request " + method + " " + path
                + " to Xeno server (model=" + (String.IsNullOrWhiteSpace(model) ? "unspecified" : model)
                + ", stream=" + (stream ? "true" : "false")
                + ", embeddings=" + (embeddings ? "true" : "false")
                + ", payloadBytes=" + Encoding.UTF8.GetByteCount(request.Body).ToString(CultureInfo.InvariantCulture)
                + ", requestId=" + request.RequestId + ").");

            LocalInferencePendingResponse pending = await LocalInferenceTunnelBridge.SendRequestAsync(request, context.Token).ConfigureAwait(false);
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(requestTimeoutMs));
                using (timeout.Token.Register(delegate { LocalInferenceTunnelBridge.CancelPending(request.RequestId, timeout.Token); }))
                using (context.Token.Register(delegate { _ = TrySendLocalInferenceCancelAsync(request.RequestId, "CallerDisconnected"); }))
                {
                    try
                    {
                        ServerLocalInferenceResponseMessage first = await WaitForFirstResponseAsync(pending, timeout.Token).ConfigureAwait(false);
                        if (stream && String.Equals(first.Type, "server.localInference.response.start", StringComparison.OrdinalIgnoreCase))
                        {
                            await StreamProxyResponseAsync(context, pending, first, route, fallback, timeout.Token).ConfigureAwait(false);
                            return;
                        }

                        await SendProxyResponseAsync(context, first, route, fallback).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!context.Token.IsCancellationRequested && timeout.IsCancellationRequested)
                    {
                        await TrySendLocalInferenceCancelAsync(request.RequestId, "RequestTimeout").ConfigureAwait(false);
                        string message = "Xeno local inference request timed out after " + requestTimeoutMs.ToString(CultureInfo.InvariantCulture) + "ms.";
                        await SendProviderErrorAsync(context, path, 504, message, "server_error", "request_timeout").ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task TrySendLocalInferenceCancelAsync(string requestId, string reason)
        {
            try
            {
                await LocalInferenceTunnelBridge.SendCancelAsync(requestId, reason, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException || ex is OperationCanceledException)
            {
            }
        }

        private static async Task<ServerLocalInferenceResponseMessage> WaitForFirstResponseAsync(LocalInferencePendingResponse pending, CancellationToken token)
        {
            using (token.Register(delegate { pending.Cancel(token); }))
            {
                Task<ServerLocalInferenceResponseMessage> completed = await Task.WhenAny(pending.Start.Task, pending.Final.Task).ConfigureAwait(false);
                return await completed.ConfigureAwait(false);
            }
        }

        private static async Task StreamProxyResponseAsync(HttpContextBase context, LocalInferencePendingResponse pending, ServerLocalInferenceResponseMessage start, string route, bool fallback, CancellationToken token)
        {
            context.Response.StatusCode = start.StatusCode ?? (start.Success ? 200 : 502);
            context.Response.ContentType = start.ContentType ?? "application/octet-stream";
            context.Response.ChunkedTransfer = true;
            ApplyResponseHeaders(context, start.Headers);
            SetRouteHeaders(context, route, fallback);
            SanitizeResponseHeaders(context);
            while (await pending.Chunks.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (pending.Chunks.Reader.TryRead(out ServerLocalInferenceChunkMessage? chunk))
                {
                    if (chunk == null) continue;
                    byte[] bytes = chunk.Decode();
                    if (bytes.Length == 0) continue;
                    await context.Response.SendChunk(bytes, false, context.Token).ConfigureAwait(false);
                }
            }

            await pending.Final.Task.ConfigureAwait(false);
            await context.Response.SendChunk(Array.Empty<byte>(), true, context.Token).ConfigureAwait(false);
        }

        private static async Task SendProxyResponseAsync(HttpContextBase context, ServerLocalInferenceResponseMessage response, string route, bool fallback)
        {
            context.Response.StatusCode = response.StatusCode ?? (response.Success ? 200 : 502);
            context.Response.ContentType = response.ContentType ?? "application/json";
            ApplyResponseHeaders(context, response.Headers);
            SetRouteHeaders(context, route, fallback);
            SanitizeResponseHeaders(context);
            await context.Response.Send(response.Body ?? "{}", context.Token).ConfigureAwait(false);
        }

        private async Task SendProviderErrorAsync(HttpContextBase context, string path, int statusCode, string message, string openAiType, string code)
        {
            context.Response.StatusCode = statusCode;
            if (path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, LocalInferenceErrorFactory.OpenAi(message, openAiType, null, code)).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                await SendJsonAsync(context, LocalInferenceErrorFactory.Ollama(message)).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(context, LocalInferenceErrorFactory.Generic(message)).ConfigureAwait(false);
        }

        private async Task<string> ReadBodyAsync(HttpContextBase context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string body = context.Request.DataAsString ?? "{}";
            if (Encoding.UTF8.GetByteCount(body) > _Settings.LocalInferenceServer.MaxRequestBodyBytes)
            {
                throw new ArgumentException("Request body exceeds the configured local inference server limit.");
            }

            await Task.CompletedTask.ConfigureAwait(false);
            return String.IsNullOrWhiteSpace(body) ? "{}" : body;
        }

        private static T RequireJson<T>(string body) where T : class
        {
            T? value = JsonUtil.Deserialize<T>(body);
            if (value == null) throw new InvalidDataException("Request body could not be read as " + typeof(T).Name + ".");
            return value;
        }

        private static Dictionary<string, string> CaptureRequestHeaders(HttpContextBase context)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? key in context.Request.Headers.AllKeys)
            {
                if (String.IsNullOrWhiteSpace(key)) continue;
                if (IsBlockedForwardedRequestHeader(key)) continue;
                headers[key] = context.Request.Headers.Get(key) ?? String.Empty;
            }

            return headers;
        }

        private static void ClearResponseHeaders(HttpContextBase context)
        {
            context.Response.Headers.Clear();
        }

        private static void SanitizeResponseHeaders(HttpContextBase context)
        {
            foreach (string header in _RequestHeaderResponseDenyList)
            {
                try
                {
                    context.Response.Headers.Remove(header);
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }

                try
                {
                    context.Request.Headers.Remove(header);
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        private static void ApplyResponseHeaders(HttpContextBase context, Dictionary<string, string> headers)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (String.IsNullOrWhiteSpace(header.Key) || String.IsNullOrWhiteSpace(header.Value)) continue;
                if (IsHopByHopHeader(header.Key)) continue;
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.Equals(RouteHeaderName, StringComparison.OrdinalIgnoreCase) || header.Key.Equals(FallbackHeaderName, StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = header.Value;
                    continue;
                }

                try
                {
                    context.Response.Headers.Add(header.Key, header.Value);
                }
                catch (ArgumentException)
                {
                }
            }
        }

        private static bool IsHopByHopHeader(string name)
        {
            return name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBlockedForwardedRequestHeader(string name)
        {
            return IsHopByHopHeader(name) ||
                   name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Expect", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyCorsHeaders(HttpContextBase context)
        {
            if (!_Settings.LocalInferenceServer.EnableCors) return;
            string origin = context.Request.Headers.Get("Origin") ?? "*";
            if (!IsAllowedCorsOrigin(origin)) origin = "null";
            context.Response.Headers.Add("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, OpenAI-Organization, x-request-id");
            context.Response.Headers.Add("Access-Control-Expose-Headers", "x-request-id, x-xeno-request-id, x-xeno-route, x-xeno-fallback, x-ratelimit-limit-requests, x-ratelimit-remaining-requests, server-timing");
            context.Response.Headers.Add("Access-Control-Max-Age", "86400");
        }

        private bool IsAllowedCorsOrigin(string origin)
        {
            if (String.IsNullOrWhiteSpace(origin) || String.Equals(origin, "*", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (string allowed in _Settings.LocalInferenceServer.CorsAllowedOrigins)
            {
                if (origin.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static async Task SendJsonAsync(HttpContextBase context, object value)
        {
            context.Response.ContentType = "application/json";
            SanitizeResponseHeaders(context);
            await context.Response.Send(JsonUtil.Serialize(value, true), context.Token).ConfigureAwait(false);
        }

        private static async Task SendHtmlAsync(HttpContextBase context, string html)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            SanitizeResponseHeaders(context);
            await context.Response.Send(html, context.Token).ConfigureAwait(false);
        }

        private static object BuildOpenApiDocument()
        {
            return new
            {
                openapi = "3.0.3",
                info = new { title = "Xeno Local Inference Server", version = "0.2.0" },
                paths = new Dictionary<string, object>
                {
                    ["/v1/chat/completions"] = new { post = new { summary = "OpenAI-compatible chat completions" } },
                    ["/v1/completions"] = new { post = new { summary = "OpenAI-compatible completions" } },
                    ["/v1/embeddings"] = new { post = new { summary = "OpenAI-compatible embeddings" } },
                    ["/v1/models"] = new { get = new { summary = "OpenAI-compatible model list" } },
                    ["/v1/models/{model}"] = new { get = new { summary = "OpenAI-compatible model details" } },
                    ["/api/generate"] = new { post = new { summary = "Ollama-compatible generation" } },
                    ["/api/chat"] = new { post = new { summary = "Ollama-compatible chat" } },
                    ["/api/embeddings"] = new { post = new { summary = "Ollama-compatible legacy embeddings" } },
                    ["/api/embed"] = new { post = new { summary = "Ollama-compatible embeddings" } },
                    ["/api/tags"] = new { get = new { summary = "Ollama-compatible model list" } },
                    ["/api/show"] = new { post = new { summary = "Ollama-compatible model details" } },
                    ["/api/ps"] = new { get = new { summary = "Ollama-compatible running models on this machine's providers" } }
                },
                xXenoRouting = "Every response carries X-Xeno-Route: local:<provider> when a provider on this machine answered, network when the Xeno network answered, local, network, or local+network for merged model lists, or unavailable. X-Xeno-Fallback: true marks a request that fell back to the network after its local provider failed before responding." 
            };
        }

        private static string SwaggerHtml()
        {
            return "<!doctype html><html><head><title>Xeno Local Inference API</title></head><body><redoc spec-url=\"/openapi.json\"></redoc><script src=\"https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js\"></script></body></html>";
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _StatusCancellation.Cancel();
                try
                {
                    _StatusTask?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                }

                _StatusCancellation.Dispose();
                LocalInferenceRuntimeStatus.Delete();
                Stop();
                IDisposable? disposable = _Webserver as IDisposable;
                disposable?.Dispose();
                _Monitor.Dispose();
                _Proxy.Dispose();
            }

            _Disposed = true;
        }
    }
}

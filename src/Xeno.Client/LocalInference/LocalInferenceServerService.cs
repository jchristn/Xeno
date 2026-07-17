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
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Watson-backed localhost OpenAI-compatible and Ollama-compatible inference listener.
    /// </summary>
    public sealed class LocalInferenceServerService : IDisposable
    {
        private readonly ClientSettings _Settings;
        private readonly Webserver _Webserver;
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
        private bool _Disposed;

        /// <summary>
        /// Instantiate the local inference server.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the listener host is not loopback.</exception>
        public LocalInferenceServerService(ClientSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
        /// Start the listener.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _Webserver.Start();
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
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentException("Local inference server host cannot be empty.", nameof(host));
            if (String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)) return;
            if (String.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase)) return;
            throw new ArgumentException("Local inference server host must be localhost, 127.0.0.1, ::1, or [::1]. Configured host: " + host, nameof(host));
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

                await HandleProviderRouteAsync(context, method, path, context.Token).ConfigureAwait(false);
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
                await SendProviderErrorAsync(context, path, 502, "Xeno websocket tunnel is unavailable: " + ex.Message, "server_error", "websocket_unavailable").ConfigureAwait(false);
            }
        }

        private async Task HandleProviderRouteAsync(HttpContextBase context, string method, string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (method == "GET" && String.Equals(path, "/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyAsync(context, path, method, "openai", null, false, false, "{}").ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path.StartsWith("/v1/models/", StringComparison.OrdinalIgnoreCase))
            {
                string model = Uri.UnescapeDataString(path.Substring("/v1/models/".Length));
                await ProxyAsync(context, path, method, "openai", model, false, false, "{}").ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OpenAiChatCompletionRequest request = RequireJson<OpenAiChatCompletionRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("OpenAI chat completion request requires model.");
                await ProxyAsync(context, path, method, "openai", request.Model, false, request.Stream.GetValueOrDefault(), body).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/v1/completions", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OpenAiCompletionRequest request = RequireJson<OpenAiCompletionRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("OpenAI completion request requires model.");
                await ProxyAsync(context, path, method, "openai", request.Model, false, request.Stream.GetValueOrDefault(), body).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/v1/embeddings", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OpenAiEmbeddingRequest request = RequireJson<OpenAiEmbeddingRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("OpenAI embeddings request requires model.");
                if (request.Input == null || request.Input.Values.Count == 0) throw new ArgumentException("OpenAI embeddings request requires input.");
                await ProxyAsync(context, path, method, "openai", request.Model, true, false, body).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/tags", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyAsync(context, path, method, "ollama", null, false, false, "{}").ConfigureAwait(false);
                return;
            }

            if (method == "GET" && String.Equals(path, "/api/ps", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyAsync(context, path, method, "ollama", null, false, false, "{}").ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/show", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaShowRequest request = RequireJson<OllamaShowRequest>(body);
                string? model = String.IsNullOrWhiteSpace(request.Model) ? request.Name : request.Model;
                if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("Ollama show request requires model.");
                await ProxyAsync(context, path, method, "ollama", model, false, false, body).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaGenerateRequest request = RequireJson<OllamaGenerateRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama generate request requires model.");
                await ProxyAsync(context, path, method, "ollama", request.Model, false, request.Stream != false, body).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/chat", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaChatRequest request = RequireJson<OllamaChatRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama chat request requires model.");
                await ProxyAsync(context, path, method, "ollama", request.Model, false, request.Stream != false, body).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/embeddings", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaLegacyEmbeddingRequest request = RequireJson<OllamaLegacyEmbeddingRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama embeddings request requires model.");
                if (String.IsNullOrWhiteSpace(request.Prompt)) throw new ArgumentException("Ollama embeddings request requires prompt.");
                await ProxyAsync(context, path, method, "ollama", request.Model, true, false, body).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && String.Equals(path, "/api/embed", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ReadBodyAsync(context, token).ConfigureAwait(false);
                OllamaEmbedRequest request = RequireJson<OllamaEmbedRequest>(body);
                if (String.IsNullOrWhiteSpace(request.Model)) throw new ArgumentException("Ollama embed request requires model.");
                if (request.Input == null || request.Input.Values.Count == 0) throw new ArgumentException("Ollama embed request requires input.");
                await ProxyAsync(context, path, method, "ollama", request.Model, true, false, body).ConfigureAwait(false);
                return;
            }

            await SendProviderErrorAsync(context, path, 404, "Route not found.", "invalid_request_error", "not_found").ConfigureAwait(false);
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

        private async Task ProxyAsync(HttpContextBase context, string path, string method, string providerFamily, string? model, bool embeddings, bool stream, string body)
        {
            int requestTimeoutMs = Math.Max(1000, _Settings.LocalInferenceServer.RequestTimeoutMs);
            ClientLocalInferenceRequestMessage request = new ClientLocalInferenceRequestMessage
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
                Body = String.IsNullOrWhiteSpace(body) ? "{}" : body
            };
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
                            await StreamProxyResponseAsync(context, pending, first, timeout.Token).ConfigureAwait(false);
                            return;
                        }

                        await SendProxyResponseAsync(context, first).ConfigureAwait(false);
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

        private static async Task StreamProxyResponseAsync(HttpContextBase context, LocalInferencePendingResponse pending, ServerLocalInferenceResponseMessage start, CancellationToken token)
        {
            context.Response.StatusCode = start.StatusCode ?? (start.Success ? 200 : 502);
            context.Response.ContentType = start.ContentType ?? "application/octet-stream";
            context.Response.ChunkedTransfer = true;
            ApplyResponseHeaders(context, start.Headers);
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

        private static async Task SendProxyResponseAsync(HttpContextBase context, ServerLocalInferenceResponseMessage response)
        {
            context.Response.StatusCode = response.StatusCode ?? (response.Success ? 200 : 502);
            context.Response.ContentType = response.ContentType ?? "application/json";
            ApplyResponseHeaders(context, response.Headers);
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
            context.Response.Headers.Add("Access-Control-Expose-Headers", "x-request-id, x-xeno-request-id, x-ratelimit-limit-requests, x-ratelimit-remaining-requests, server-timing");
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
                info = new { title = "Xeno Local Inference Server", version = "0.1.0" },
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
                    ["/api/ps"] = new { get = new { summary = "Ollama-compatible running models" } }
                }
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
                Stop();
                IDisposable? disposable = _Webserver as IDisposable;
                disposable?.Dispose();
            }

            _Disposed = true;
        }
    }
}

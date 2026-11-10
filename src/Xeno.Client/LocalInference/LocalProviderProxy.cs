namespace Xeno.Client.LocalInference
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Forwards local inference requests directly to a provider on this machine, streaming the response back as it arrives.
    /// Bodies are forwarded as bytes and never inspected or logged.
    /// Thread safe; one instance serves concurrent requests.
    /// </summary>
    public sealed class LocalProviderProxy : IDisposable
    {
        private const int BufferSize = 8192;
        private readonly HttpClient _Http;
        private bool _Disposed;

        /// <summary>
        /// Create a proxy.
        /// </summary>
        /// <param name="handler">Optional HTTP handler, for tests. When null the proxy uses a default handler.</param>
        public LocalProviderProxy(HttpMessageHandler? handler = null)
        {
            _Http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            _Http.Timeout = Timeout.InfiniteTimeSpan;
        }

        /// <summary>
        /// Forward a request to a provider and write the provider's response to the caller.
        /// </summary>
        /// <param name="context">Listener request context.</param>
        /// <param name="provider">Provider to call.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Request path, for example "/v1/chat/completions". Appended to the provider endpoint.</param>
        /// <param name="body">Request body as received. Ignored for GET.</param>
        /// <param name="stream">Whether the caller asked for a streaming response.</param>
        /// <param name="timeoutMs">Request timeout in milliseconds. Minimum 1000.</param>
        /// <param name="applyHeaders">Called just before response headers are sent, to add listener headers such as X-Xeno-Route.</param>
        /// <param name="token">Cancellation token; cancels the upstream request when the caller disconnects.</param>
        /// <returns>Outcome.</returns>
        /// <exception cref="ArgumentNullException">context or provider is null.</exception>
        public async Task<LocalProviderProxyResult> ForwardAsync(HttpContextBase context, ClientRunnerSettings provider, string method, string path, string body, bool stream, int timeoutMs, Action<HttpContextBase> applyHeaders, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(provider);
            LocalProviderProxyResult result = new LocalProviderProxyResult();
            string url = ProviderConfiguration.NormalizeEndpoint(provider.Endpoint) + path;
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (HttpRequestMessage request = BuildRequest(provider, method, url, body))
            {
                timeout.CancelAfter(Math.Max(1000, timeoutMs));
                System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                HttpResponseMessage response;
                try
                {
                    response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    result.FirstByteMs = clock.Elapsed.TotalMilliseconds;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is InvalidOperationException || ex is UriFormatException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                {
                    result.Error = ex is OperationCanceledException
                        ? "Provider '" + provider.Name + "' did not respond within " + Math.Max(1000, timeoutMs) + " ms."
                        : "Provider '" + provider.Name + "' is unavailable: " + ex.Message;
                    return result;
                }

                using (response)
                {
                    result.StatusCode = (int)response.StatusCode;
                    context.Response.StatusCode = (int)response.StatusCode;
                    string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                    context.Response.ContentType = contentType;
                    CopySafeHeaders(context, response);
                    applyHeaders?.Invoke(context);
                    bool streaming = stream || IsStreamingContentType(contentType);
                    if (!streaming)
                    {
                        byte[] bytes;
                        try
                        {
                            bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is HttpRequestException || ex is IOException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                        {
                            result.Error = "Provider '" + provider.Name + "' failed while sending its response: " + ex.Message;
                            return result;
                        }

                        result.Started = true;
                        result.ResponseBytes = bytes.Length;
                        Xeno.Client.Routing.LocalUsageProbe? usage = Xeno.Client.Routing.LocalUsageProbe.Read(bytes);
                        result.PromptTokens = usage?.PromptTokens;
                        result.CompletionTokens = usage?.CompletionTokens;
                        await context.Response.Send(bytes, context.Token).ConfigureAwait(false);
                        result.Completed = true;
                        return result;
                    }

                    context.Response.ChunkedTransfer = true;
                    result.Started = true;
                    try
                    {
                        using (Stream upstream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
                        {
                            byte[] buffer = new byte[BufferSize];
                            while (true)
                            {
                                int read = await upstream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token).ConfigureAwait(false);
                                if (read <= 0) break;
                                byte[] chunk = new byte[read];
                                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                                result.ResponseBytes += read;
                                await context.Response.SendChunk(chunk, false, context.Token).ConfigureAwait(false);
                            }
                        }

                        await context.Response.SendChunk(Array.Empty<byte>(), true, context.Token).ConfigureAwait(false);
                        result.Completed = true;
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is IOException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                    {
                        result.Error = "Provider '" + provider.Name + "' stopped in the middle of a streaming response.";
                        await TrySendStreamErrorAsync(context, path, contentType, result.Error).ConfigureAwait(false);
                    }

                    return result;
                }
            }
        }

        /// <summary>
        /// GET a JSON document from a provider and read it as a named type.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="provider">Provider.</param>
        /// <param name="path">Path, for example "/api/tags".</param>
        /// <param name="timeoutMs">Timeout in milliseconds. Minimum 500.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Parsed document, or null when the provider failed or returned an error.</returns>
        /// <exception cref="ArgumentNullException">provider is null.</exception>
        public async Task<T?> GetJsonAsync<T>(ClientRunnerSettings provider, string path, int timeoutMs, CancellationToken token) where T : class
        {
            ArgumentNullException.ThrowIfNull(provider);
            string url = ProviderConfiguration.NormalizeEndpoint(provider.Endpoint) + path;
            try
            {
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (HttpRequestMessage request = BuildRequest(provider, "GET", url, String.Empty))
                {
                    timeout.CancelAfter(Math.Max(500, timeoutMs));
                    using (HttpResponseMessage response = await _Http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return null;
                        string json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                        return JsonUtil.Deserialize<T>(json);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is InvalidOperationException || ex is UriFormatException || ex is JsonException || (ex is OperationCanceledException && !token.IsCancellationRequested))
            {
                return null;
            }
        }

        /// <summary>
        /// Release the HTTP client.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Http.Dispose();
        }

        private static HttpRequestMessage BuildRequest(ClientRunnerSettings provider, string method, string url, string body)
        {
            HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(method), url);
            if (!String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) && !String.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                request.Content = new StringContent(String.IsNullOrWhiteSpace(body) ? "{}" : body, Encoding.UTF8, "application/json");
            }

            if (!String.IsNullOrWhiteSpace(provider.Credential))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.Credential);
            }

            return request;
        }

        private static void CopySafeHeaders(HttpContextBase context, HttpResponseMessage response)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                if (!IsSafeResponseHeader(header.Key)) continue;
                try
                {
                    context.Response.Headers.Add(header.Key, String.Join(", ", header.Value));
                }
                catch (ArgumentException)
                {
                }
            }
        }

        private static bool IsSafeResponseHeader(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return false;
            if (name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith("X-Xeno-", StringComparison.OrdinalIgnoreCase)) return false;
            switch (name.ToLowerInvariant())
            {
                case "connection":
                case "keep-alive":
                case "proxy-authenticate":
                case "proxy-authorization":
                case "te":
                case "trailer":
                case "transfer-encoding":
                case "upgrade":
                case "content-length":
                case "set-cookie":
                case "server":
                case "date":
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsStreamingContentType(string contentType)
        {
            return contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("application/x-ndjson", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task TrySendStreamErrorAsync(HttpContextBase context, string path, string contentType, string message)
        {
            try
            {
                string payload;
                if (path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase) || contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    payload = "data: " + JsonUtil.Serialize(LocalInferenceErrorFactory.OpenAi(message, "server_error", null, "upstream_interrupted")) + "\n\n";
                }
                else
                {
                    payload = JsonUtil.Serialize(LocalInferenceErrorFactory.Ollama(message)) + "\n";
                }

                await context.Response.SendChunk(Encoding.UTF8.GetBytes(payload), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Array.Empty<byte>(), true, context.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is OperationCanceledException || ex is ObjectDisposedException)
            {
            }
        }
    }
}

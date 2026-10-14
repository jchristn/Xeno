namespace Xeno.Client.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Minimal REST client for the Xeno server, used by every client front end for sign-in, connectivity checks, and
    /// discovering the models the Xeno network offers. Instances are thread-safe; dispose when finished.
    /// </summary>
    public sealed class XenoServerClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Path of the server ping endpoint.
        /// </summary>
        public const string PingPath = "/v1.0/api/ping";

        /// <summary>
        /// Path of the server login endpoint.
        /// </summary>
        public const string LoginPath = "/v1.0/api/auth/login";

        /// <summary>
        /// Path of the OpenAI-compatible model list.
        /// </summary>
        public const string ModelsPath = "/v1/models";

        /// <summary>
        /// Default request timeout in milliseconds.
        /// </summary>
        public const int DefaultTimeoutMs = 15000;

        #endregion

        #region Private-Members

        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsClient;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a server client.
        /// </summary>
        /// <param name="handler">Optional HTTP handler, used by tests. The client does not dispose a supplied handler.</param>
        public XenoServerClient(HttpMessageHandler? handler = null)
        {
            _HttpClient = handler == null ? new HttpClient() : new HttpClient(handler, false);
            _HttpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            _OwnsClient = true;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Sign in with a username and password, then store the issued tenant-bound client credential in the settings
        /// and save them.
        /// </summary>
        /// <param name="settings">Client settings. Updated and saved on success.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password. Never logged.</param>
        /// <param name="tenantId">Tenant to sign in to, or null for the configured tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Login result. Server and network failures are reported in the result.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="ArgumentException">Username or password is empty.</exception>
        public async Task<LoginResult> LoginAsync(ClientSettings settings, string username, string password, string? tenantId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
            if (String.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));
            string effectiveTenant = String.IsNullOrWhiteSpace(tenantId) ? settings.TenantId : tenantId;
            string clientId = ServerEndpoints.EnsureClientId(settings);
            Uri endpoint = ServerEndpoints.BuildRestUri(settings, LoginPath);
            try
            {
                using (CancellationTokenSource timeout = CreateTimeout(token, DefaultTimeoutMs))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.Add("x-tenant-guid", effectiveTenant);
                    request.Content = new StringContent(JsonUtil.Serialize(new LoginRequestBody
                    {
                        Username = username,
                        Password = password,
                        TenantId = effectiveTenant,
                        ClientId = clientId,
                        Label = settings.Name
                    }), Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            return new LoginResult { Success = false, StatusCode = (int)response.StatusCode, Error = FormatHttpError(response, body) };
                        }

                        LoginCredentialResponse? credential = TryDeserialize<LoginCredentialResponse>(body);
                        if (credential == null || String.IsNullOrWhiteSpace(credential.AccessKey))
                        {
                            return new LoginResult { Success = false, StatusCode = (int)response.StatusCode, Error = "Login response did not include a client credential." };
                        }

                        settings.TenantId = String.IsNullOrWhiteSpace(credential.TenantId) ? effectiveTenant : credential.TenantId;
                        settings.Server.AccessKey = credential.AccessKey;
                        settings.Server.SecretKey = null;
                        settings.Server.Token = null;
                        settings.Save();
                        return new LoginResult { Success = true, StatusCode = (int)response.StatusCode, TenantId = settings.TenantId };
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return new LoginResult { Success = false, Error = "Login timed out." };
            }
            catch (HttpRequestException ex)
            {
                return new LoginResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Send one HTTP heartbeat to the server ping endpoint.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="sequence">Sequence number, starting at 1.</param>
        /// <param name="timeoutMs">Timeout in milliseconds (100 to 600000).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Attempt result. Timeouts and network failures are reported in the result.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Timeout is outside 100 to 600000.</exception>
        public async Task<PingAttemptResult> PingAsync(ClientSettings settings, int sequence, int timeoutMs, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (timeoutMs < 100 || timeoutMs > 600000) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be from 100 through 600000 milliseconds.");
            Uri endpoint = ServerEndpoints.BuildRestUri(settings, PingPath);
            Uri requestUri = ServerEndpoints.BuildPingUri(endpoint, sequence, DateTime.UtcNow);
            PingAttemptResult result = new PingAttemptResult { Sequence = sequence };
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                using (CancellationTokenSource timeout = CreateTimeout(token, timeoutMs))
                using (HttpResponseMessage response = await _HttpClient.GetAsync(requestUri, timeout.Token).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                    stopwatch.Stop();
                    result.ElapsedMs = stopwatch.ElapsedMilliseconds;
                    result.Bytes = Encoding.UTF8.GetByteCount(body);
                    result.StatusCode = (int)response.StatusCode;
                    result.ReasonPhrase = response.ReasonPhrase;
                    result.Success = response.IsSuccessStatusCode;
                    ClientPingResponse? ping = TryDeserialize<ClientPingResponse>(body);
                    if (ping != null)
                    {
                        result.Status = ping.Status ?? String.Empty;
                        result.ServerUtc = ping.ServerUtc ?? String.Empty;
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                stopwatch.Stop();
                result.ElapsedMs = timeoutMs;
                result.Error = "Timeout";
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                result.ElapsedMs = stopwatch.ElapsedMilliseconds;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// List the models the Xeno network offers this client's tenant, authenticating with the stored access key.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result. Server and network failures are reported in the result.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public async Task<NetworkModelListResult> ListNetworkModelsAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (!ServerEndpoints.HasWebSocketCredential(settings))
            {
                return new NetworkModelListResult { Success = false, Error = "Sign in to a Xeno server to see the models the network offers." };
            }

            try
            {
                Uri endpoint = ServerEndpoints.BuildRestUri(settings, ModelsPath);
                using (CancellationTokenSource timeout = CreateTimeout(token, DefaultTimeoutMs))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
                {
                    request.Headers.Add("x-access-key", settings.Server.AccessKey);
                    request.Headers.Add("x-tenant-guid", settings.TenantId);
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            return new NetworkModelListResult { Success = false, StatusCode = (int)response.StatusCode, Error = FormatHttpError(response, body) };
                        }

                        NetworkModelListResponse? list = TryDeserialize<NetworkModelListResponse>(body);
                        return new NetworkModelListResult
                        {
                            Success = list != null,
                            StatusCode = (int)response.StatusCode,
                            Models = list?.Data ?? new System.Collections.Generic.List<NetworkModelEntry>(),
                            Error = list == null ? "The server returned an unreadable model list." : null
                        };
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return new NetworkModelListResult { Success = false, Error = "The Xeno server did not respond in time." };
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is UriFormatException)
            {
                return new NetworkModelListResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Format an unsuccessful HTTP response as "status reason - message", preferring the server's error message.
        /// </summary>
        /// <param name="response">Response.</param>
        /// <param name="responseBody">Response body.</param>
        /// <returns>Formatted error, at most about 300 characters of message.</returns>
        /// <exception cref="ArgumentNullException">Response is null.</exception>
        public static string FormatHttpError(HttpResponseMessage response, string? responseBody)
        {
            ArgumentNullException.ThrowIfNull(response);
            string body = responseBody ?? String.Empty;
            string? message = TryGetErrorMessage(body);
            if (String.IsNullOrWhiteSpace(message)) message = body.Trim();
            if (String.IsNullOrWhiteSpace(message)) message = response.ReasonPhrase ?? "Request failed";
            if (body.Contains("plain HTTP request was sent to HTTPS port", StringComparison.OrdinalIgnoreCase))
            {
                string host = response.RequestMessage?.RequestUri?.Authority ?? "the server";
                return ((int)response.StatusCode) + " " + response.ReasonPhrase + " - The server only accepts HTTPS. Use https://" + host + " (or wss:// for the WebSocket URL) as the server address.";
            }

            if (message.Length > 300) message = message.Substring(0, 300) + "...";
            return ((int)response.StatusCode) + " " + response.ReasonPhrase + " - " + message;
        }

        /// <summary>
        /// Dispose the client.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            if (_OwnsClient) _HttpClient.Dispose();
        }

        #endregion

        #region Private-Methods

        private static CancellationTokenSource CreateTimeout(CancellationToken token, int timeoutMs)
        {
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(token);
            source.CancelAfter(timeoutMs);
            return source;
        }

        private static string? TryGetErrorMessage(string responseBody)
        {
            ClientErrorResponse? error = TryDeserialize<ClientErrorResponse>(responseBody);
            return error?.Message ?? error?.Error ?? error?.Title ?? error?.Detail;
        }

        private static T? TryDeserialize<T>(string body) where T : class
        {
            if (String.IsNullOrWhiteSpace(body)) return null;
            try
            {
                return JsonUtil.TryDeserialize<T>(body, out T? value) ? value : null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        #endregion
    }
}

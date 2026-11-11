namespace Xeno.Client.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;
    using Xeno.Core.Analytics;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Reads usage analytics and provider health from the running client's local API, for the desktop app, terminal
    /// app, CLI, and MCP tools. Every surface draws the same responses the web dashboard does. Thread safe.
    /// </summary>
    public sealed class XenoUsageClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Message when the client is not running.
        /// </summary>
        public const string NotRunningMessage = "Usage needs the Xeno client running with its local dashboard enabled. Start the client and try again.";

        /// <summary>
        /// True when a running client published its local API.
        /// </summary>
        public bool IsAvailable
        {
            get { return _Endpoint() != null; }
        }

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;
        private readonly Func<LocalApiEndpoint?> _Endpoint;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the client.
        /// </summary>
        /// <param name="endpoint">Returns the local API endpoint, or null to read ~/.xeno/local-api.json.</param>
        /// <param name="handler">HTTP handler, for tests; null for the default.</param>
        public XenoUsageClient(Func<LocalApiEndpoint?>? endpoint = null, HttpMessageHandler? handler = null)
        {
            _Endpoint = endpoint ?? LocalApiEndpoint.Read;
            _Http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            _Http.Timeout = TimeSpan.FromMinutes(2);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Local API path for a dataset's usage analytics.
        /// </summary>
        /// <param name="dataset">ServedRequests, LocalEndpoint, or Chat.</param>
        /// <returns>Path relative to /api/v1/.</returns>
        /// <exception cref="ArgumentException">The dataset is not served by the local API.</exception>
        public static string RelativePath(UsageDatasetEnum dataset)
        {
            switch (dataset)
            {
                case UsageDatasetEnum.ServedRequests: return "requests/analytics/usage";
                case UsageDatasetEnum.LocalEndpoint: return "local-inference/analytics/usage";
                case UsageDatasetEnum.Chat: return "chat/analytics/usage";
                default: throw new ArgumentException("Network request usage is on the Xeno server, not this machine.", nameof(dataset));
            }
        }

        /// <summary>
        /// Usage analytics for a dataset on this machine.
        /// </summary>
        /// <param name="dataset">ServedRequests, LocalEndpoint, or Chat.</param>
        /// <param name="query">Query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics.</returns>
        /// <exception cref="ArgumentNullException">query is null.</exception>
        /// <exception cref="InvalidOperationException">The client is not running.</exception>
        /// <exception cref="ArgumentException">The query or dataset is not valid.</exception>
        public Task<UsageAnalytics> GetUsageAsync(UsageDatasetEnum dataset, UsageAnalyticsQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            return GetAsync<UsageAnalytics>(RelativePath(dataset) + "?" + UsageQueryParser.ToQueryString(query), token);
        }

        /// <summary>
        /// Provider health strip for this machine's providers.
        /// </summary>
        /// <param name="query">Query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Strip.</returns>
        /// <exception cref="ArgumentNullException">query is null.</exception>
        /// <exception cref="InvalidOperationException">The client is not running.</exception>
        public Task<ProviderHealthStrip> GetProviderHealthAsync(ProviderHealthQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            return GetAsync<ProviderHealthStrip>("telemetry/providers/health?" + UsageQueryParser.ToQueryString(query), token);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Http.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task<T> GetAsync<T>(string relative, CancellationToken token) where T : class
        {
            LocalApiEndpoint endpoint = _Endpoint() ?? throw new InvalidOperationException(NotRunningMessage);
            using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, endpoint.Url.TrimEnd('/') + "/api/v1/" + relative))
            {
                message.Headers.Add("X-Xeno-Local-Token", endpoint.Token);
                HttpResponseMessage response;
                try
                {
                    response = await _Http.SendAsync(message, token).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    throw new InvalidOperationException(NotRunningMessage);
                }

                using (response)
                {
                    string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return JsonUtil.Deserialize<T>(text) ?? throw new InvalidDataException("The client returned an empty response.");
                    string reason = response.ReasonPhrase ?? "Request failed.";
                    if (JsonUtil.TryDeserialize<LocalDashboardErrorResponse>(text, out LocalDashboardErrorResponse? error) && !String.IsNullOrWhiteSpace(error?.Message)) reason = error!.Message;
                    if (response.StatusCode == HttpStatusCode.BadRequest) throw new ArgumentException(reason);
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("The client's local API rejected the token. Restart the client and try again.");
                    throw new InvalidOperationException(reason);
                }
            }
        }

        #endregion
    }
}

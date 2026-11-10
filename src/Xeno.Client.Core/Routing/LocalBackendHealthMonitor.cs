namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Checks the health of every routable local provider on an interval and records the models each one reports.
    /// Real request failures also mark a provider unhealthy, so the router reacts before the next check.
    /// Thread safe: snapshots are replaced atomically and readers never see a partially updated status.
    /// </summary>
    public sealed class LocalBackendHealthMonitor : IDisposable
    {
        private const int MaximumProbeTimeoutMs = 5000;
        private readonly Func<ClientSettings> _Settings;
        private readonly HttpClient _Http;
        private readonly bool _OwnsHttp;
        private readonly object _Lock = new object();
        private Dictionary<string, LocalBackendStatus> _Statuses = new Dictionary<string, LocalBackendStatus>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _LoopCancellation;
        private Task? _LoopTask;
        private bool _Disposed;

        /// <summary>
        /// Create a monitor.
        /// </summary>
        /// <param name="settings">Returns the current client settings; called on every check so setting changes take effect without a restart.</param>
        /// <param name="handler">Optional HTTP handler, for tests. When null the monitor owns a default handler.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public LocalBackendHealthMonitor(Func<ClientSettings> settings, HttpMessageHandler? handler = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            _OwnsHttp = true;
            _Http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        }

        /// <summary>
        /// Latest status for every provider that has been checked or used. Never null.
        /// </summary>
        public IReadOnlyCollection<LocalBackendStatus> Statuses
        {
            get
            {
                lock (_Lock)
                {
                    return _Statuses.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Status for one provider, or null when it has never been checked or used.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <returns>Status or null.</returns>
        public LocalBackendStatus? Get(string provider)
        {
            lock (_Lock)
            {
                return _Statuses.TryGetValue(provider ?? String.Empty, out LocalBackendStatus? status) ? status : null;
            }
        }

        /// <summary>
        /// Start checking on the interval in <see cref="ClientLocalInferenceServerSettings.HealthCheckIntervalMs"/>. The first check runs immediately. Calling Start twice has no effect.
        /// </summary>
        public void Start()
        {
            lock (_Lock)
            {
                if (_Disposed) throw new ObjectDisposedException(nameof(LocalBackendHealthMonitor));
                if (_LoopTask != null) return;
                _LoopCancellation = new CancellationTokenSource();
                CancellationToken token = _LoopCancellation.Token;
                _LoopTask = Task.Run(async () => await LoopAsync(token).ConfigureAwait(false), token);
            }
        }

        /// <summary>
        /// Check every routable provider now.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statuses after the check.</returns>
        public async Task<IReadOnlyCollection<LocalBackendStatus>> CheckNowAsync(CancellationToken token)
        {
            ClientSettings settings = _Settings();
            List<ClientRunnerSettings> providers = LocalInferenceRouter.RoutableProviders(settings);
            await Task.WhenAll(providers.Select(item => CheckAsync(item, token))).ConfigureAwait(false);
            lock (_Lock)
            {
                Dictionary<string, LocalBackendStatus> kept = new Dictionary<string, LocalBackendStatus>(StringComparer.OrdinalIgnoreCase);
                foreach (ClientRunnerSettings provider in providers)
                {
                    if (_Statuses.TryGetValue(provider.Name, out LocalBackendStatus? status)) kept[provider.Name] = status;
                }

                _Statuses = kept;
            }

            return Statuses;
        }

        /// <summary>
        /// Record a failed request so the router treats the provider as unhealthy until the next successful check.
        /// </summary>
        /// <param name="provider">Provider settings.</param>
        /// <param name="error">Error message. Must not contain prompt content.</param>
        public void MarkFailure(ClientRunnerSettings provider, string error)
        {
            if (provider == null) return;
            LocalBackendStatus? previous = Get(provider.Name);
            Publish(new LocalBackendStatus
            {
                Provider = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                Healthy = false,
                LastCheckedUtc = DateTime.UtcNow,
                LastLatencyMs = previous?.LastLatencyMs,
                LastError = error ?? String.Empty,
                DiscoveredModels = previous?.DiscoveredModels ?? new List<string>()
            });
        }

        /// <summary>
        /// Record a successful request against a provider that was marked unhealthy.
        /// </summary>
        /// <param name="provider">Provider settings.</param>
        public void MarkSuccess(ClientRunnerSettings provider)
        {
            if (provider == null) return;
            LocalBackendStatus? previous = Get(provider.Name);
            if (previous != null && previous.Healthy == true) return;
            Publish(new LocalBackendStatus
            {
                Provider = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                Healthy = true,
                LastCheckedUtc = DateTime.UtcNow,
                LastLatencyMs = previous?.LastLatencyMs,
                LastError = String.Empty,
                DiscoveredModels = previous?.DiscoveredModels ?? new List<string>()
            });
        }

        /// <summary>
        /// Stop the background loop and release the HTTP client.
        /// </summary>
        public void Dispose()
        {
            Task? loop;
            lock (_Lock)
            {
                if (_Disposed) return;
                _Disposed = true;
                _LoopCancellation?.Cancel();
                loop = _LoopTask;
            }

            try
            {
                loop?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _LoopCancellation?.Dispose();
            if (_OwnsHttp) _Http.Dispose();
        }

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckNowAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }

                int interval = _Settings().LocalInferenceServer?.HealthCheckIntervalMs ?? 15000;
                try
                {
                    await Task.Delay(Math.Max(1000, interval), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task CheckAsync(ClientRunnerSettings provider, CancellationToken token)
        {
            bool ollama = ProviderConfiguration.IsOllamaApiType(provider.ApiType);
            string url = ProviderConfiguration.NormalizeEndpoint(provider.Endpoint) + (ollama ? "/api/tags" : "/v1/models");
            int timeoutMs = Math.Min(MaximumProbeTimeoutMs, Math.Max(1000, provider.TimeoutMs));
            Stopwatch stopwatch = Stopwatch.StartNew();
            LocalBackendStatus status = new LocalBackendStatus
            {
                Provider = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint
            };
            try
            {
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    timeout.CancelAfter(timeoutMs);
                    if (!String.IsNullOrWhiteSpace(provider.Credential)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.Credential);
                    using (HttpResponseMessage response = await _Http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        string json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                        stopwatch.Stop();
                        status.LastLatencyMs = stopwatch.Elapsed.TotalMilliseconds;
                        status.LastCheckedUtc = DateTime.UtcNow;
                        if (!response.IsSuccessStatusCode)
                        {
                            status.Healthy = false;
                            status.LastError = "Health check returned HTTP " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
                        }
                        else
                        {
                            status.Healthy = true;
                            status.DiscoveredModels = ollama ? ParseOllamaModels(json) : ParseOpenAiModels(json);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is InvalidOperationException || ex is UriFormatException || ex is JsonException)
            {
                if (token.IsCancellationRequested) throw;
                status.Healthy = false;
                status.LastCheckedUtc = DateTime.UtcNow;
                status.LastError = ex is OperationCanceledException ? "Health check timed out after " + timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms." : ex.Message;
                status.DiscoveredModels = Get(provider.Name)?.DiscoveredModels ?? new List<string>();
            }

            Publish(status);
        }

        private void Publish(LocalBackendStatus status)
        {
            lock (_Lock)
            {
                Dictionary<string, LocalBackendStatus> next = new Dictionary<string, LocalBackendStatus>(_Statuses, StringComparer.OrdinalIgnoreCase);
                next[status.Provider] = status;
                _Statuses = next;
            }
        }

        private static List<string> ParseOllamaModels(string json)
        {
            LocalOllamaTagsResponse? tags = JsonUtil.Deserialize<LocalOllamaTagsResponse>(json);
            return (tags?.Models ?? new List<LocalOllamaModel>())
                .Select(item => String.IsNullOrWhiteSpace(item.Name) ? item.Model : item.Name)
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseOpenAiModels(string json)
        {
            LocalOpenAiModelList? list = JsonUtil.Deserialize<LocalOpenAiModelList>(json);
            return (list?.Data ?? new List<LocalOpenAiModel>())
                .Select(item => item.Id)
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}

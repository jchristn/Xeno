namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// The one way every front end starts, lists, and cancels model pulls. Pulls belong to the Xeno client worker, the
    /// long-lived process, so a pull started from the CLI, the desktop app, the terminal app, or the web dashboard keeps
    /// running when that front end closes, and every front end sees the same list. Ollama stops a pull when its caller
    /// disconnects, which is why the caller must be the worker rather than the front end.
    /// When the worker is not running, pulls fall back to this process (<see cref="ModelPullCoordinator.Shared"/>) and
    /// <see cref="RunsInWorker"/> is false, so a front end can say the pull ends when it exits.
    /// Thread safe.
    /// </summary>
    public sealed class XenoModelPullClient : IDisposable
    {
        /// <summary>
        /// How often <see cref="JobChanged"/> polls the worker while a pull is active.
        /// </summary>
        public static readonly TimeSpan ActivePollInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// How often <see cref="JobChanged"/> polls the worker while nothing is active, to notice pulls other front ends start.
        /// </summary>
        public static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(10);

        private readonly Func<LocalApiEndpoint?> _Endpoint;
        private readonly HttpClient _Http;
        private readonly ModelPullCoordinator _Local;
        private readonly object _Lock = new object();
        private readonly Dictionary<string, ModelPullJob> _Last = new Dictionary<string, ModelPullJob>(StringComparer.Ordinal);
        private List<ModelPullJob> _RemoteCache = new List<ModelPullJob>();
        private EventHandler<ModelPullJob>? _JobChanged;
        private CancellationTokenSource? _PollCancellation;
        private bool _Disposed;

        /// <summary>
        /// Create a client.
        /// </summary>
        /// <param name="endpoint">Where the worker's local API is; defaults to the published local-api.json.</param>
        /// <param name="handler">Optional HTTP handler, for tests.</param>
        /// <param name="local">Coordinator used when the worker is not running; defaults to <see cref="ModelPullCoordinator.Shared"/>.</param>
        public XenoModelPullClient(Func<LocalApiEndpoint?>? endpoint = null, HttpMessageHandler? handler = null, ModelPullCoordinator? local = null)
        {
            _Endpoint = endpoint ?? LocalApiEndpoint.Read;
            _Http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            _Http.Timeout = TimeSpan.FromSeconds(15);
            _Local = local ?? ModelPullCoordinator.Shared;
        }

        /// <summary>
        /// True when pulls go to the client worker and survive this process. Read again before each use; the worker can start or stop.
        /// </summary>
        public bool RunsInWorker => _Endpoint() != null;

        /// <summary>
        /// Raised on a background thread when a pull starts, progresses, or ends, whichever process runs it. Subscribing
        /// starts polling the worker; unsubscribing the last handler stops it. Handlers must not block.
        /// </summary>
        public event EventHandler<ModelPullJob>? JobChanged
        {
            add
            {
                lock (_Lock)
                {
                    bool first = _JobChanged == null;
                    _JobChanged += value;
                    if (first && !_Disposed) StartPolling();
                }
            }

            remove
            {
                lock (_Lock)
                {
                    _JobChanged -= value;
                    if (_JobChanged == null) StopPolling();
                }
            }
        }

        /// <summary>
        /// Start pulling a model onto an Ollama provider. An identical active pull is returned instead of starting another.
        /// </summary>
        /// <param name="providerName">Provider name.</param>
        /// <param name="modelName">Model to pull.</param>
        /// <param name="advertise">Advertise the model once the pull succeeds.</param>
        /// <param name="token">Cancellation token for the request; it does not cancel the pull.</param>
        /// <returns>Snapshot of the job.</returns>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        /// <exception cref="NotSupportedException">The provider is not Ollama.</exception>
        /// <exception cref="ArgumentException">The model name is missing.</exception>
        public async Task<ModelPullJob> StartAsync(string providerName, string modelName, bool advertise, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            LocalApiEndpoint? endpoint = _Endpoint();
            if (endpoint != null)
            {
                ModelPullJob? remote = await TrySendAsync<ModelPullJob>(endpoint, HttpMethod.Post, "providers/" + Uri.EscapeDataString(providerName) + "/pulls", new ModelPullRequest { Model = modelName.Trim(), Advertise = advertise }, token).ConfigureAwait(false);
                if (remote != null)
                {
                    Remember(remote);
                    Wake();
                    return remote;
                }
            }

            ClientSettings settings = ClientSettings.Load();
            ClientRunnerSettings provider = ProviderConfiguration.FindProvider(settings, providerName) ?? throw new KeyNotFoundException("Provider endpoint not found: " + providerName);
            Func<ModelPullJob, CancellationToken, Task>? onSucceeded = null;
            if (advertise)
            {
                onSucceeded = (job, callbackToken) =>
                {
                    ClientSettings latest = ClientSettings.Load();
                    bool embeddings = ProviderModelManager.LooksLikeEmbeddingModel(job.Model, null);
                    ModelImportResult result = ProviderModelManager.ImportModels(latest, new List<ModelImportItem> { new ModelImportItem { Provider = job.Provider, Name = job.Model, SupportsCompletions = !embeddings, SupportsEmbeddings = embeddings } });
                    if (result.Added.Count > 0) ProviderConfiguration.SaveInventory(latest);
                    if (result.Failed.Count > 0) throw new InvalidOperationException(String.Join("; ", result.Failed));
                    return Task.CompletedTask;
                };
            }

            ModelPullJob local = _Local.Start(provider, modelName, advertise, onSucceeded);
            Wake();
            return local;
        }

        /// <summary>
        /// Every pull, newest first: the worker's when it is running, plus any this process ran itself.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshots.</returns>
        public async Task<List<ModelPullJob>> ListAsync(CancellationToken token)
        {
            List<ModelPullJob> jobs = _Local.List();
            LocalApiEndpoint? endpoint = _Endpoint();
            if (endpoint != null)
            {
                ModelPullJobListResponse? remote = await TrySendAsync<ModelPullJobListResponse>(endpoint, HttpMethod.Get, "model-pulls", null, token).ConfigureAwait(false);
                if (remote != null)
                {
                    lock (_Lock)
                    {
                        _RemoteCache = remote.Jobs.Select(item => item.Clone()).ToList();
                    }

                    jobs.AddRange(remote.Jobs.Where(item => jobs.All(existing => existing.Id != item.Id)));
                }
            }

            return jobs.OrderByDescending(item => item.StartedUtc).ToList();
        }

        /// <summary>
        /// Wait for a pull to end, polling the worker when it runs there.
        /// </summary>
        /// <param name="id">Job id.</param>
        /// <param name="progress">Called with each new snapshot.</param>
        /// <param name="token">Cancellation token; cancelling stops the wait, not the pull.</param>
        /// <returns>Final snapshot, or null when the job is unknown.</returns>
        public async Task<ModelPullJob?> WaitAsync(string id, Action<ModelPullJob>? progress, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                ModelPullJob? job = _Local.Get(id);
                if (job == null)
                {
                    LocalApiEndpoint? endpoint = _Endpoint();
                    if (endpoint != null) job = await TrySendAsync<ModelPullJob>(endpoint, HttpMethod.Get, "model-pulls/" + Uri.EscapeDataString(id), null, token).ConfigureAwait(false);
                }

                if (job == null) return null;
                progress?.Invoke(job);
                if (!job.Active) return job;
                await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Cancel an active pull, wherever it runs.
        /// </summary>
        /// <param name="id">Job id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a pull was found.</returns>
        public async Task<bool> CancelAsync(string id, CancellationToken token)
        {
            if (_Local.Cancel(id)) return true;
            LocalApiEndpoint? endpoint = _Endpoint();
            if (endpoint == null) return false;
            ModelPullJob? job = await TrySendAsync<ModelPullJob>(endpoint, HttpMethod.Delete, "model-pulls/" + Uri.EscapeDataString(id), null, token).ConfigureAwait(false);
            if (job != null) Remember(job);
            Wake();
            return job != null;
        }

        /// <summary>
        /// Forget finished pulls, wherever they ran.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number removed.</returns>
        public async Task<int> ClearFinishedAsync(CancellationToken token)
        {
            int removed = _Local.ClearFinished();
            LocalApiEndpoint? endpoint = _Endpoint();
            if (endpoint != null)
            {
                LocalDashboardMutationResponse? response = await TrySendAsync<LocalDashboardMutationResponse>(endpoint, HttpMethod.Post, "model-pulls/clear", new ModelPullRequest(), token).ConfigureAwait(false);
                removed += response?.Removed ?? 0;
            }

            return removed;
        }

        /// <summary>
        /// Every pull as last seen, newest first, without a network call: this process's pulls plus the worker's pulls from
        /// the latest poll, start, or list. Keep <see cref="JobChanged"/> subscribed for the worker's part to stay fresh.
        /// </summary>
        /// <returns>Snapshots.</returns>
        public List<ModelPullJob> Snapshot()
        {
            List<ModelPullJob> jobs = _Local.List();
            lock (_Lock)
            {
                jobs.AddRange(_RemoteCache.Where(item => jobs.All(existing => existing.Id != item.Id)).Select(item => item.Clone()));
            }

            return jobs.OrderByDescending(item => item.StartedUtc).ToList();
        }

        /// <summary>
        /// Stop polling and release the HTTP client. Pulls keep running.
        /// </summary>
        public void Dispose()
        {
            lock (_Lock)
            {
                if (_Disposed) return;
                _Disposed = true;
                StopPolling();
            }

            _Local.JobChanged -= OnLocalJobChanged;
            _Http.Dispose();
        }

        private void StartPolling()
        {
            _Local.JobChanged += OnLocalJobChanged;
            _PollCancellation = new CancellationTokenSource();
            CancellationToken token = _PollCancellation.Token;
            _ = Task.Run(() => PollAsync(token));
        }

        private void StopPolling()
        {
            _Local.JobChanged -= OnLocalJobChanged;
            if (_PollCancellation == null) return;
            _PollCancellation.Cancel();
            _PollCancellation.Dispose();
            _PollCancellation = null;
        }

        private void Wake()
        {
            lock (_Lock)
            {
                if (_JobChanged == null || _Disposed) return;
                StopPolling();
                StartPolling();
            }
        }

        private async Task PollAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool active = false;
                try
                {
                    LocalApiEndpoint? endpoint = _Endpoint();
                    if (endpoint != null)
                    {
                        ModelPullJobListResponse? remote = await TrySendAsync<ModelPullJobListResponse>(endpoint, HttpMethod.Get, "model-pulls", null, token).ConfigureAwait(false);
                        if (remote != null)
                        {
                            lock (_Lock)
                            {
                                _RemoteCache = remote.Jobs.Select(item => item.Clone()).ToList();
                            }
                        }

                        foreach (ModelPullJob job in remote?.Jobs ?? new List<ModelPullJob>())
                        {
                            active |= job.Active;
                            RaiseIfChanged(job);
                        }
                    }

                    active |= _Local.List().Any(item => item.Active);
                    await Task.Delay(active ? ActivePollInterval : IdlePollInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is InvalidDataException || ex is InvalidOperationException)
                {
                    try
                    {
                        await Task.Delay(IdlePollInterval, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private void Remember(ModelPullJob job)
        {
            lock (_Lock)
            {
                _RemoteCache.RemoveAll(item => item.Id == job.Id);
                _RemoteCache.Add(job.Clone());
            }

            RaiseIfChanged(job);
        }

        private void OnLocalJobChanged(object? sender, ModelPullJob job)
        {
            RaiseIfChanged(job);
        }

        private void RaiseIfChanged(ModelPullJob job)
        {
            EventHandler<ModelPullJob>? handler;
            lock (_Lock)
            {
                if (_Last.TryGetValue(job.Id, out ModelPullJob? previous)
                    && previous.UpdatedUtc == job.UpdatedUtc
                    && previous.State == job.State) return;
                _Last[job.Id] = job.Clone();
                handler = _JobChanged;
            }

            if (handler == null) return;
            try
            {
                handler(this, job);
            }
            catch (Exception)
            {
                // A front end's progress handler must never stop polling.
            }
        }

        private async Task<T?> TrySendAsync<T>(LocalApiEndpoint endpoint, HttpMethod method, string relative, object? body, CancellationToken token) where T : class
        {
            using (HttpRequestMessage message = new HttpRequestMessage(method, endpoint.Url.TrimEnd('/') + "/api/v1/" + relative))
            {
                message.Headers.Add("X-Xeno-Local-Token", endpoint.Token);
                if (body != null) message.Content = new StringContent(JsonUtil.Serialize(body), Encoding.UTF8, "application/json");
                HttpResponseMessage response;
                try
                {
                    response = await _Http.SendAsync(message, token).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    return null;
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested)
                {
                    return null;
                }

                using (response)
                {
                    string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return JsonUtil.TryDeserialize(text, out T? value) ? value : null;
                    if (response.StatusCode == HttpStatusCode.NotFound && method == HttpMethod.Get) return null;
                    string reason = response.ReasonPhrase ?? "Request failed.";
                    if (JsonUtil.TryDeserialize(text, out LocalDashboardErrorResponse? error) && !String.IsNullOrWhiteSpace(error?.Message)) reason = error!.Message;
                    if (response.StatusCode == HttpStatusCode.BadRequest) throw new NotSupportedException(reason);
                    if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException(reason);
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("The client's local API rejected the token. Restart the client and try again.");
                    throw new InvalidOperationException(reason);
                }
            }
        }
    }
}

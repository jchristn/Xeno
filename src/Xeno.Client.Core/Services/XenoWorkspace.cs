namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Hosting;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// The single entry point the desktop app and the terminal app use for everything they show and do: client
    /// status and lifecycle, providers and models, the unified model catalog, request history, telemetry, logs, the
    /// Xeno server account, and local settings. Front ends stay thin views over this class, so behavior is identical
    /// across surfaces and future changes to how models are exposed (local or through the Xeno network) land here once.
    /// Methods are safe to call from any thread; configuration writes are serialized.
    /// </summary>
    public sealed class XenoWorkspace : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Number of request history rows loaded by default.
        /// </summary>
        public int RequestPageSize
        {
            get { return _RequestPageSize; }
            set { _RequestPageSize = Math.Clamp(value, 1, 5000); }
        }

        #endregion

        #region Private-Members

        private const int RolledUpLogReadFactor = 20;
        private const int RolledUpLogLinesLimit = 50000;
        private readonly SemaphoreSlim _ConfigLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _DataLock = new SemaphoreSlim(1, 1);
        private readonly XenoServerClient _Server;
        private readonly Func<XenoExecutableLocation?> _CliLocator;
        private LocalDataSession? _Data = null;
        private Chat.XenoChatClient? _Chat = null;
        private Analytics.XenoUsageClient? _Usage = null;
        private int _RequestPageSize = 200;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a workspace.
        /// </summary>
        /// <param name="server">Server client, or null to create one.</param>
        /// <param name="cliLocator">Locates the xeno executable used to start the worker, or null for <see cref="XenoExecutableLocator"/>.</param>
        public XenoWorkspace(XenoServerClient? server = null, Func<XenoExecutableLocation?>? cliLocator = null)
        {
            _Server = server ?? new XenoServerClient();
            _CliLocator = cliLocator ?? (() => XenoExecutableLocator.Locate(XenoApplicationEnum.Cli));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The running client's chat API: conversations, streaming turns, feedback, and history.
        /// </summary>
        public Chat.XenoChatClient Chat
        {
            get
            {
                if (_Chat == null) _Chat = new Chat.XenoChatClient();
                return _Chat;
            }
        }

        /// <summary>
        /// Usage analytics and provider health from the running client's local API, shared by the Usage and provider
        /// telemetry pages.
        /// </summary>
        public Analytics.XenoUsageClient Usage
        {
            get
            {
                if (_Usage == null) _Usage = new Analytics.XenoUsageClient();
                return _Usage;
            }
        }

        /// <summary>
        /// Capture the current client status.
        /// </summary>
        /// <returns>Snapshot.</returns>
        public ClientStatusSnapshot GetStatus()
        {
            return ClientStatusSnapshot.Capture();
        }

        /// <summary>
        /// Model names a provider reported the last time the running listener checked it (for example 'ollama list'),
        /// when model discovery is on. Empty when the client is not running or discovery is off.
        /// </summary>
        /// <param name="providerName">Provider.</param>
        /// <returns>Model names.</returns>
        public IReadOnlyList<string> DiscoveredModels(string providerName)
        {
            ClientSettings settings = LoadSettings();
            if (!(settings.LocalInferenceServer?.DiscoverLocalModels ?? true)) return new List<string>();
            Routing.LocalInferenceRuntimeStatus? status = Routing.LocalInferenceRuntimeStatus.Load();
            Routing.LocalBackendStatus? backend = status?.Backends.FirstOrDefault(item => String.Equals(item.Provider, providerName, StringComparison.OrdinalIgnoreCase));
            return backend?.DiscoveredModels ?? new List<string>();
        }

        /// <summary>
        /// Load the current settings from client.json.
        /// </summary>
        /// <returns>Settings.</returns>
        public ClientSettings LoadSettings()
        {
            return ClientSettings.Load();
        }

        /// <summary>
        /// Start the client worker.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Start result.</returns>
        /// <exception cref="InvalidOperationException">The xeno executable cannot be found, no access key is configured, or the process cannot start.</exception>
        public async Task<ClientStartResult> StartClientAsync(CancellationToken token)
        {
            XenoExecutableLocation cli = _CliLocator() ?? throw new InvalidOperationException("Could not find the xeno command. Install it with 'dotnet tool install -g xeno' or set " + XenoExecutableLocator.CliEnvironmentVariable + ".");
            return await ClientWorkerController.StartAsync(LoadSettings(), cli, ClientWorkerController.DefaultStartTimeoutMs, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the client worker.
        /// </summary>
        /// <param name="force">True to terminate it if it does not exit in time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Stop result.</returns>
        public Task<ClientStopResult> StopClientAsync(bool force, CancellationToken token)
        {
            return ClientWorkerController.StopAsync(force, ClientWorkerController.DefaultStopTimeoutMs, token);
        }

        /// <summary>
        /// Restart the client worker.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Start result.</returns>
        /// <exception cref="InvalidOperationException">The worker did not stop, or cannot be started.</exception>
        public async Task<ClientStartResult> RestartClientAsync(CancellationToken token)
        {
            ClientStopResult stop = await StopClientAsync(true, token).ConfigureAwait(false);
            if (!stop.Stopped) throw new InvalidOperationException("The client did not stop, so it was not restarted.");
            return await StartClientAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Ask a running worker to re-advertise providers and models on its next heartbeat.
        /// </summary>
        public void RequestInventoryUpdate()
        {
            ClientConnectionState.MarkInventoryUpdateRequested();
        }

        /// <summary>
        /// Build the unified catalog of local and network models.
        /// </summary>
        /// <param name="includeNetwork">True to ask the Xeno server which models the network offers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Catalog.</returns>
        public async Task<ModelCatalog> GetModelCatalogAsync(bool includeNetwork, CancellationToken token)
        {
            ClientSettings settings = LoadSettings();
            if (!includeNetwork) return ModelCatalogBuilder.BuildLocal(settings);
            return await ModelCatalogBuilder.BuildAsync(settings, _Server, token).ConfigureAwait(false);
        }

        /// <summary>
        /// The Local endpoint page's data: listener, route rules, and provider health and concurrency.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Routing view.</returns>
        public Task<Routing.LocalInferenceRoutingView> GetLocalInferenceRoutingAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Routing.LocalInferenceRoutingService.BuildView(LoadSettings(), Routing.LocalInferenceRuntimeStatus.Load()));
        }

        /// <summary>
        /// Where a model would go, using live provider health when the client is running.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Decision.</returns>
        public Task<Routing.LocalInferenceRouteDecision> ResolveRouteAsync(string model, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Routing.LocalInferenceRoutingService.Resolve(LoadSettings(), Routing.LocalInferenceRuntimeStatus.Load(), model));
        }

        /// <summary>
        /// Add or replace a route rule and save. Routing changes apply to the running listener without a restart.
        /// </summary>
        /// <param name="rule">Rule.</param>
        /// <param name="originalModel">Model of the rule being edited, or null for a new rule.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Stored rule.</returns>
        /// <exception cref="ArgumentException">The rule is invalid.</exception>
        public Task<LocalInferenceRouteRule> SaveRouteRuleAsync(LocalInferenceRouteRule rule, string? originalModel, CancellationToken token)
        {
            return MutateAsync(settings => new MutationResult<LocalInferenceRouteRule>(Routing.LocalInferenceRoutingService.SaveRule(settings, rule, originalModel), false), token);
        }

        /// <summary>
        /// Delete the route rule for a model and save.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rules removed.</returns>
        public Task<int> DeleteRouteRuleAsync(string model, CancellationToken token)
        {
            return MutateAsync(settings => new MutationResult<int>(Routing.LocalInferenceRoutingService.DeleteRule(settings, model), false), token);
        }

        /// <summary>
        /// Check every routable provider now from this process.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statuses.</returns>
        public Task<IReadOnlyCollection<Routing.LocalBackendStatus>> CheckBackendsAsync(CancellationToken token)
        {
            return Routing.LocalInferenceRoutingService.CheckBackendsAsync(LoadSettings(), token);
        }

        /// <summary>
        /// Recent local inference listener requests, newest first. Metadata only.
        /// </summary>
        /// <param name="skip">Records to skip.</param>
        /// <param name="take">Records to return, 1 through 1000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Records.</returns>
        public Task<List<Routing.LocalInferenceRequestRecord>> GetLocalInferenceRequestsAsync(int skip, int take, CancellationToken token)
        {
            return Routing.LocalInferenceRequestLog.ReadAsync(skip, take, token);
        }

        /// <summary>
        /// List configured providers.
        /// </summary>
        /// <returns>Rows.</returns>
        public List<ProviderRow> GetProviders()
        {
            return LoadSettings().Runners.Select(ProviderRow.From).ToList();
        }

        /// <summary>
        /// Add a provider endpoint and save.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Add result.</returns>
        /// <exception cref="ArgumentException">The provider is invalid or loops back to the local inference server.</exception>
        public Task<ProviderAddResult> AddProviderAsync(ClientRunnerSettings provider, CancellationToken token)
        {
            return MutateAsync(settings =>
            {
                settings.ValidateProviderEndpointAgainstLocalInference(provider.Name, provider.Endpoint);
                ProviderAddResult result = ProviderConfiguration.AddProvider(settings, provider);
                return new MutationResult<ProviderAddResult>(result, result.Added);
            }, token);
        }

        /// <summary>
        /// Update a provider endpoint and save.
        /// </summary>
        /// <param name="name">Current provider name.</param>
        /// <param name="update">Changes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated provider.</returns>
        /// <exception cref="ArgumentException">The update is invalid.</exception>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        public Task<ClientRunnerSettings> UpdateProviderAsync(string name, ProviderUpdateRequest update, CancellationToken token)
        {
            return MutateAsync(settings =>
            {
                ClientRunnerSettings runner = ProviderConfiguration.UpdateProvider(settings, name, update);
                settings.ValidateProviderEndpointAgainstLocalInference(runner.Name, runner.Endpoint);
                return new MutationResult<ClientRunnerSettings>(runner, true);
            }, token);
        }

        /// <summary>
        /// Delete a provider and its models, then save.
        /// </summary>
        /// <param name="name">Provider name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number removed.</returns>
        public Task<int> DeleteProviderAsync(string name, CancellationToken token)
        {
            return MutateAsync(settings =>
            {
                int removed = ProviderConfiguration.DeleteProvider(settings, name);
                return new MutationResult<int>(removed, removed > 0);
            }, token);
        }

        /// <summary>
        /// Add a model to a provider and save.
        /// </summary>
        /// <param name="providerName">Provider name.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="supportsCompletions">Serves completions.</param>
        /// <param name="supportsEmbeddings">Serves embeddings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider.</returns>
        /// <exception cref="ArgumentException">The model is invalid.</exception>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        public Task<ClientRunnerSettings> AddModelAsync(string providerName, string modelName, bool supportsCompletions, bool supportsEmbeddings, CancellationToken token)
        {
            return MutateAsync(settings =>
            {
                ClientRunnerSettings provider = ProviderConfiguration.FindProvider(settings, providerName) ?? throw new KeyNotFoundException("Provider endpoint not found: " + providerName);
                if (provider.Models.Any(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Model already exists in provider endpoint '" + provider.Name + "': " + modelName);
                }

                return new MutationResult<ClientRunnerSettings>(ProviderConfiguration.AddModel(settings, providerName, modelName, supportsCompletions, supportsEmbeddings), true);
            }, token);
        }

        /// <summary>
        /// Update, rename, or move a model and save.
        /// </summary>
        /// <param name="update">Changes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Update result.</returns>
        /// <exception cref="ArgumentException">The update is invalid.</exception>
        /// <exception cref="KeyNotFoundException">The model or target provider does not exist.</exception>
        public Task<ModelUpdateResult> UpdateModelAsync(ModelUpdateRequest update, CancellationToken token)
        {
            return MutateAsync(settings => new MutationResult<ModelUpdateResult>(ProviderConfiguration.UpdateModel(settings, update), true), token);
        }

        /// <summary>
        /// Delete a model and save.
        /// </summary>
        /// <param name="providerName">Provider name, or null for every provider.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number removed.</returns>
        public Task<int> DeleteModelAsync(string? providerName, string modelName, CancellationToken token)
        {
            return MutateAsync(settings =>
            {
                int removed = ProviderConfiguration.DeleteModel(settings, providerName, modelName);
                return new MutationResult<int>(removed, removed > 0);
            }, token);
        }

        /// <summary>
        /// Test a provider's model-list endpoint.
        /// </summary>
        /// <param name="name">Provider name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Test result.</returns>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        public async Task<LocalProviderTestResponse> TestProviderAsync(string name, CancellationToken token)
        {
            ClientRunnerSettings provider = ProviderConfiguration.FindProvider(LoadSettings(), name) ?? throw new KeyNotFoundException("Provider endpoint not found: " + name);
            return await ProviderConfiguration.TestProviderAsync(provider, true, Math.Min(provider.TimeoutMs, 10000), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Request history statistics.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statistics.</returns>
        public Task<LocalDashboardStatisticsResponse> GetStatisticsAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.RequestHistory.StatisticsAsync(token), token);
        }

        /// <summary>
        /// Most recent requests, newest first.
        /// </summary>
        /// <param name="limit">Maximum rows, or 0 for <see cref="RequestPageSize"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows.</returns>
        public Task<List<RequestRow>> ListRequestsAsync(int limit, CancellationToken token)
        {
            int effective = limit <= 0 ? RequestPageSize : limit;
            return WithDataAsync(async data =>
            {
                IReadOnlyList<LocalRequestHistoryEntry> entries = await data.RequestHistory.ListAsync(effective, token).ConfigureAwait(false);
                return entries.Select(RequestRow.From).ToList();
            }, token);
        }

        /// <summary>
        /// One request with its timeline, headers, and bodies.
        /// </summary>
        /// <param name="id">Request identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry, or null when not found.</returns>
        public Task<LocalRequestHistoryEntry?> GetRequestAsync(string id, CancellationToken token)
        {
            return WithDataAsync(data => data.RequestHistory.GetAsync(id, token), token);
        }

        /// <summary>
        /// Delete one request from history.
        /// </summary>
        /// <param name="id">Request identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when deleted.</returns>
        public Task<bool> DeleteRequestAsync(string id, CancellationToken token)
        {
            return WithDataAsync(data => data.RequestHistory.DeleteAsync(id, token), token);
        }

        /// <summary>
        /// Delete all request history.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number deleted.</returns>
        public Task<int> ClearRequestsAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.RequestHistory.DeleteAllAsync(token), token);
        }

        /// <summary>
        /// Request counts over time.
        /// </summary>
        /// <param name="range">Range.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Buckets, oldest first.</returns>
        public Task<List<LocalRequestHistorySummaryBucket>> SummarizeRequestsAsync(HistoryRangeEnum range, CancellationToken token)
        {
            return WithDataAsync(data => data.SummarizeAsync(range, token), token);
        }

        /// <summary>
        /// Describe the local history database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database description.</returns>
        public Task<LocalDashboardDatabaseResponse> DescribeDatabaseAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.DescribeDatabaseAsync(token), token);
        }

        /// <summary>
        /// Latest stored host telemetry sample.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sample detail, or null.</returns>
        public Task<ClientTelemetrySampleDetail?> GetLatestHostTelemetryAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.TelemetryHistory.ReadLatestAsync(token), token);
        }

        /// <summary>
        /// Host telemetry storage status.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        public Task<ClientTelemetryPersistenceStatus> GetHostTelemetryStatusAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.TelemetryHistory.GetStatusAsync(data.Settings.Telemetry, token), token);
        }

        /// <summary>
        /// Collect and store a host telemetry sample now.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshot, or null.</returns>
        public Task<ClientTelemetrySnapshot?> CollectHostTelemetryAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.CollectHostTelemetryAsync(token), token);
        }

        /// <summary>
        /// Host telemetry rollups for a metric over a range.
        /// </summary>
        /// <param name="metric">Metric.</param>
        /// <param name="range">Range.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Buckets.</returns>
        public Task<List<ClientTelemetryRollupBucket>> RollupHostTelemetryAsync(ClientTelemetryMetricEnum metric, HistoryRangeEnum range, CancellationToken token)
        {
            DateTime toUtc = DateTime.UtcNow;
            ClientTelemetryRollupRequest request = new ClientTelemetryRollupRequest
            {
                FromUtc = HistoryRanges.StartUtc(range, toUtc),
                ToUtc = toUtc,
                BucketMinutes = HistoryRanges.DefaultBucketMinutes(range),
                Metric = metric
            };
            return WithDataAsync(async data => (await data.TelemetryHistory.RollupAsync(request, token).ConfigureAwait(false)).ToList(), token);
        }

        /// <summary>
        /// Host telemetry samples over a range, newest first.
        /// </summary>
        /// <param name="range">Range.</param>
        /// <param name="limit">Maximum rows (1 to 1000).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows.</returns>
        public Task<List<HostSampleRow>> SearchHostTelemetryAsync(HistoryRangeEnum range, int limit, CancellationToken token)
        {
            DateTime toUtc = DateTime.UtcNow;
            ClientTelemetrySearchRequest request = new ClientTelemetrySearchRequest
            {
                FromUtc = HistoryRanges.StartUtc(range, toUtc),
                ToUtc = toUtc,
                Limit = Math.Clamp(limit, 1, 1000)
            };
            return WithDataAsync(async data => (await data.TelemetryHistory.SearchAsync(request, token).ConfigureAwait(false)).Samples.Select(HostSampleRow.From).ToList(), token);
        }

        /// <summary>
        /// Delete all host telemetry samples.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number deleted.</returns>
        public Task<int> ClearHostTelemetryAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.TelemetryHistory.DeleteAllAsync(token), token);
        }

        /// <summary>
        /// Latest stored provider telemetry, one row per provider.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows.</returns>
        public Task<List<ProviderSampleRow>> GetLatestProviderTelemetryAsync(CancellationToken token)
        {
            return WithDataAsync(async data =>
            {
                List<ProviderSampleRow> rows = new List<ProviderSampleRow>();
                foreach (ClientRunnerSettings provider in data.Settings.Runners)
                {
                    ClientProviderTelemetrySampleDetail? detail = await data.ProviderTelemetryHistory.ReadLatestAsync(provider.Name, token).ConfigureAwait(false);
                    if (detail != null) rows.Add(ProviderSampleRow.From(detail.Sample));
                }

                return rows;
            }, token);
        }

        /// <summary>
        /// Latest stored provider telemetry detail for one provider, including models.
        /// </summary>
        /// <param name="providerName">Provider name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detail, or null.</returns>
        public Task<ClientProviderTelemetrySampleDetail?> GetProviderTelemetryDetailAsync(string providerName, CancellationToken token)
        {
            return WithDataAsync(data => data.ProviderTelemetryHistory.ReadLatestAsync(providerName, token), token);
        }

        /// <summary>
        /// Collect and store provider telemetry now.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Live rows from the collection.</returns>
        public Task<List<ProviderSampleRow>> CollectProviderTelemetryAsync(CancellationToken token)
        {
            return WithDataAsync(async data =>
            {
                ClientProviderTelemetrySnapshot? snapshot = await data.CollectProviderTelemetryAsync(token).ConfigureAwait(false);
                return snapshot == null ? new List<ProviderSampleRow>() : snapshot.Providers.Select(ProviderSampleRow.From).ToList();
            }, token);
        }

        /// <summary>
        /// Provider telemetry rollups for a metric over a range.
        /// </summary>
        /// <param name="metric">Metric.</param>
        /// <param name="providerName">Provider name, or null for all providers.</param>
        /// <param name="range">Range.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="modelName">Only this model, or null for every model.</param>
        /// <returns>Buckets.</returns>
        public Task<List<ClientProviderTelemetryRollupBucket>> RollupProviderTelemetryAsync(ClientProviderTelemetryMetricEnum metric, string? providerName, HistoryRangeEnum range, CancellationToken token, string? modelName = null)
        {
            DateTime toUtc = DateTime.UtcNow;
            ClientProviderTelemetryRollupRequest request = new ClientProviderTelemetryRollupRequest
            {
                FromUtc = HistoryRanges.StartUtc(range, toUtc),
                ToUtc = toUtc,
                BucketMinutes = HistoryRanges.DefaultBucketMinutes(range),
                ProviderName = String.IsNullOrWhiteSpace(providerName) ? null : providerName,
                ModelName = String.IsNullOrWhiteSpace(modelName) ? null : modelName,
                Metric = metric
            };
            return WithDataAsync(async data => (await data.ProviderTelemetryHistory.RollupAsync(request, token).ConfigureAwait(false)).ToList(), token);
        }

        /// <summary>
        /// Provider telemetry samples over a range, newest first.
        /// </summary>
        /// <param name="providerName">Provider name, or null for all providers.</param>
        /// <param name="range">Range.</param>
        /// <param name="limit">Maximum rows (1 to 1000).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows.</returns>
        public Task<List<ProviderSampleRow>> SearchProviderTelemetryAsync(string? providerName, HistoryRangeEnum range, int limit, CancellationToken token)
        {
            DateTime toUtc = DateTime.UtcNow;
            ClientProviderTelemetrySearchRequest request = new ClientProviderTelemetrySearchRequest
            {
                FromUtc = HistoryRanges.StartUtc(range, toUtc),
                ToUtc = toUtc,
                ProviderName = String.IsNullOrWhiteSpace(providerName) ? null : providerName,
                Limit = Math.Clamp(limit, 1, 1000)
            };
            return WithDataAsync(async data => (await data.ProviderTelemetryHistory.SearchAsync(request, token).ConfigureAwait(false)).Samples.Select(ProviderSampleRow.From).ToList(), token);
        }

        /// <summary>
        /// Delete all provider telemetry samples.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number deleted.</returns>
        public Task<int> ClearProviderTelemetryAsync(CancellationToken token)
        {
            return WithDataAsync(data => data.ProviderTelemetryHistory.DeleteAllAsync(token), token);
        }

        /// <summary>
        /// Read the worker log.
        /// </summary>
        /// <param name="tail">Maximum lines.</param>
        /// <param name="severity">Severity filter (all, info, warn, error, debug).</param>
        /// <param name="errorLog">True to read the error log instead of the main log.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Lines, oldest first, with routine server messages rolled up (see <see cref="ClientLogRollup"/>).</returns>
        public Task<List<string>> ReadLogAsync(int tail, string severity, bool errorLog, CancellationToken token)
        {
            return ReadLogAsync(tail, severity, errorLog, null, token);
        }

        /// <summary>
        /// Read the last lines of the worker log written at or after a time, filtered by severity.
        /// </summary>
        /// <param name="tail">Maximum lines.</param>
        /// <param name="severity">Severity filter (all, info, warn, error, debug).</param>
        /// <param name="errorLog">True to read the error log instead of the main log.</param>
        /// <param name="sinceUtc">Earliest time to include, or null for any time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Lines, oldest first, with routine server messages rolled up.</returns>
        public async Task<List<string>> ReadLogAsync(int tail, string severity, bool errorLog, DateTime? sinceUtc, CancellationToken token)
        {
            if (tail <= 0) return new List<string>();
            int raw = (int)Math.Min(RolledUpLogLinesLimit, (long)tail * RolledUpLogReadFactor);
            List<string> lines = await ClientLogReader.ReadTailAsync(errorLog ? ClientConnectionState.ErrorLogFile : ClientConnectionState.LogFile, raw, severity, sinceUtc, token).ConfigureAwait(false);
            List<string> collapsed = ClientLogRollup.Collapse(lines);
            return collapsed.Count <= tail ? collapsed : collapsed.GetRange(collapsed.Count - tail, tail);
        }

        /// <summary>
        /// Sign in to the Xeno server and store the issued credential.
        /// </summary>
        /// <param name="serverUrl">Server to sign in to (see <see cref="ServerTargets.Resolve"/>), or null to keep the current one. A different server replaces the configured WebSocket and REST URLs.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        /// <param name="tenantId">Tenant, or null for the configured tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Login result.</returns>
        public async Task<LoginResult> SignInAsync(string? serverUrl, string username, string password, string? tenantId, CancellationToken token)
        {
            await _ConfigLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ClientSettings settings = LoadSettings();
                try
                {
                    ServerTargets.ApplySignInServer(settings, serverUrl);
                }
                catch (ArgumentException ex)
                {
                    return new LoginResult { Success = false, Error = ex.Message };
                }

                return await _Server.LoginAsync(settings, username, password, tenantId, token).ConfigureAwait(false);
            }
            finally
            {
                _ConfigLock.Release();
            }
        }

        /// <summary>
        /// Send HTTP heartbeats to the Xeno server.
        /// </summary>
        /// <param name="count">Number of attempts (1 to 20).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Attempts.</returns>
        public async Task<List<PingAttemptResult>> PingServerAsync(int count, CancellationToken token)
        {
            ClientSettings settings = LoadSettings();
            List<PingAttemptResult> attempts = new List<PingAttemptResult>();
            int effective = Math.Clamp(count, 1, 20);
            for (int sequence = 1; sequence <= effective; sequence++)
            {
                attempts.Add(await _Server.PingAsync(settings, sequence, 5000, token).ConfigureAwait(false));
                if (sequence < effective) await Task.Delay(250, token).ConfigureAwait(false);
            }

            return attempts;
        }

        /// <summary>
        /// Update the server connection settings and save.
        /// </summary>
        /// <param name="webSocketUrl">WebSocket URL (ws:// or wss://).</param>
        /// <param name="restUrl">REST URL, or empty to derive it from the WebSocket URL.</param>
        /// <param name="clientName">Client display name.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentException">A URL is malformed or the name is empty.</exception>
        public Task UpdateServerAsync(string webSocketUrl, string? restUrl, string clientName, string tenantId, CancellationToken token)
        {
            if (!Uri.TryCreate(webSocketUrl, UriKind.Absolute, out Uri? ws) || (ws.Scheme != "ws" && ws.Scheme != "wss"))
            {
                throw new ArgumentException("The WebSocket URL must start with ws:// or wss://.");
            }

            if (!String.IsNullOrWhiteSpace(restUrl) && (!Uri.TryCreate(restUrl, UriKind.Absolute, out Uri? rest) || (rest.Scheme != "http" && rest.Scheme != "https")))
            {
                throw new ArgumentException("The REST URL must start with http:// or https://.");
            }

            if (String.IsNullOrWhiteSpace(clientName)) throw new ArgumentException("The client name is required.");
            return MutateAsync(settings =>
            {
                settings.Server.Url = webSocketUrl.Trim();
                settings.Server.RestUrl = String.IsNullOrWhiteSpace(restUrl) ? null : restUrl.Trim();
                settings.Name = clientName.Trim();
                if (!String.IsNullOrWhiteSpace(tenantId)) settings.TenantId = tenantId.Trim();
                return new MutationResult<bool>(true, true);
            }, token);
        }

        /// <summary>
        /// Point this client at a Xeno server and save. Accepts the values <see cref="ServerTargets.Resolve"/> accepts,
        /// including "cloud" for the Xeno cloud server. The worker picks up the change the next time it starts.
        /// </summary>
        /// <param name="value">Server value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Resolved target.</returns>
        /// <exception cref="ArgumentException">The value is not a valid server.</exception>
        public Task<ServerTarget> UseServerAsync(string value, CancellationToken token)
        {
            ServerTarget target = ServerTargets.Resolve(value);
            return MutateAsync(settings =>
            {
                settings.Server.Url = target.WebSocketUrl;
                settings.Server.RestUrl = target.RestUrl;
                return new MutationResult<ServerTarget>(target, false);
            }, token);
        }

        /// <summary>
        /// Store an access key and save, clearing other credentials.
        /// </summary>
        /// <param name="accessKey">Access key, or null to sign out.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task SetAccessKeyAsync(string? accessKey, CancellationToken token)
        {
            return MutateAsync(settings =>
            {
                settings.Server.AccessKey = String.IsNullOrWhiteSpace(accessKey) ? null : accessKey.Trim();
                settings.Server.SecretKey = null;
                settings.Server.Token = null;
                return new MutationResult<bool>(true, false);
            }, token);
        }

        /// <summary>
        /// Apply local listener, history, and telemetry settings and save. Listener changes require a restart.
        /// </summary>
        /// <param name="request">Settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentException">A host is not allowed or a provider would loop back.</exception>
        public async Task UpdateLocalSettingsAsync(LocalClientSettingsUpdateRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            await MutateAsync(settings =>
            {
                LocalSettingsEditor.Apply(settings, request);
                return new MutationResult<bool>(true, false);
            }, token).ConfigureAwait(false);
            ResetData();
        }

        /// <summary>
        /// Apply edited values from <see cref="XenoSettingsCatalog"/> and save.
        /// </summary>
        /// <param name="values">Values by setting key; missing keys keep their current values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a changed value requires restarting the client.</returns>
        /// <exception cref="ArgumentException">A value is invalid, a host is not allowed, or a provider would loop back.</exception>
        public async Task<bool> UpdateSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(values);
            ClientSettings current = LoadSettings();
            Dictionary<string, string> before = XenoSettingsCatalog.Read(current);
            LocalClientSettingsUpdateRequest request = XenoSettingsCatalog.BuildRequest(current, values);
            await UpdateLocalSettingsAsync(request, token).ConfigureAwait(false);
            Dictionary<string, string> after = XenoSettingsCatalog.Read(LoadSettings());
            return XenoSettingsCatalog.RequiresRestart(before, after);
        }

        /// <summary>
        /// Dispose the workspace and close the local database.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            ResetData();
            _Server.Dispose();
            _Chat?.Dispose();
            _Usage?.Dispose();
            _ConfigLock.Dispose();
            _DataLock.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task<T> MutateAsync<T>(Func<ClientSettings, MutationResult<T>> mutate, CancellationToken token)
        {
            await _ConfigLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ClientSettings settings = LoadSettings();
                MutationResult<T> result = mutate(settings);
                if (result.InventoryChanged) ProviderConfiguration.SaveInventory(settings);
                else settings.Save();
                return result.Value;
            }
            finally
            {
                _ConfigLock.Release();
            }
        }

        private async Task<T> WithDataAsync<T>(Func<LocalDataSession, Task<T>> action, CancellationToken token)
        {
            LocalDataSession data = await GetDataAsync(token).ConfigureAwait(false);
            return await action(data).ConfigureAwait(false);
        }

        private async Task<LocalDataSession> GetDataAsync(CancellationToken token)
        {
            if (_Data != null) return _Data;
            await _DataLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_Data == null) _Data = await LocalDataSession.OpenAsync(LoadSettings(), token).ConfigureAwait(false);
                return _Data;
            }
            finally
            {
                _DataLock.Release();
            }
        }

        private void ResetData()
        {
            LocalDataSession? data = _Data;
            _Data = null;
            data?.Dispose();
        }

        #endregion
    }
}

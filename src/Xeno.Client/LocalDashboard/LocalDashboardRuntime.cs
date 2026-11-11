namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Client.Telemetry.Providers;
    using Xeno.Client.Telemetry.Services;
    using Xeno.Core.Models;

    /// <summary>
    /// Local dashboard runtime bridge for the Xeno client worker.
    /// </summary>
    public static class LocalDashboardRuntime
    {
        private static readonly ConcurrentDictionary<string, LocalRequestHistoryEntry> _InFlight = new ConcurrentDictionary<string, LocalRequestHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        private static LocalDashboardDatabaseDriverBase? _Driver;
        private static ILocalRequestHistoryMethods? _History;
        private static IClientTelemetryHistoryMethods? _TelemetryHistory;
        private static IClientProviderTelemetryHistoryMethods? _ProviderTelemetryHistory;
        private static ClientTelemetryPersistenceService? _TelemetryService;
        private static ClientProviderTelemetryPersistenceService? _ProviderTelemetryService;
        private static LocalDashboardService? _Service;
        private static Xeno.Client.LocalInference.RoutedChatTransport? _ChatTransport;
        private static Xeno.Client.Services.XenoWorkspace? _ToolWorkspace;
        private static Xeno.Client.Tools.Workspace.Processes.BackgroundProcessRegistry? _Processes;
        private static Xeno.Client.Tools.Mcp.McpClientManager? _Mcp;
        private static Xeno.Client.Tools.Workspace.Planning.TaskPlanStore? _Plans;
        private static CancellationTokenSource? _IngestCancel;

        /// <summary>
        /// Xeno tools for the in-app chat and the MCP server, or null when the local dashboard is not running.
        /// </summary>
        public static Xeno.Client.Tools.XenoToolCatalog? Tools { get; private set; } = null;

        /// <summary>
        /// Every tool chat offers (Xeno, workspace, and external MCP tools), or null when chat is not running.
        /// </summary>
        public static Xeno.Client.Tools.XenoChatToolHost? ChatTools { get; private set; } = null;

        /// <summary>
        /// Tools from external MCP servers, or null when chat is not running.
        /// </summary>
        public static Xeno.Client.Tools.Mcp.IMcpToolSource? McpTools { get; private set; } = null;

        /// <summary>
        /// Local dashboard history database.
        /// </summary>
        public static ILocalRequestHistoryMethods? History
        {
            get
            {
                return _History;
            }
        }

        /// <summary>
        /// Local telemetry history database.
        /// </summary>
        public static IClientTelemetryHistoryMethods? TelemetryHistory
        {
            get
            {
                return _TelemetryHistory;
            }
        }

        /// <summary>
        /// Local provider telemetry history database.
        /// </summary>
        public static IClientProviderTelemetryHistoryMethods? ProviderTelemetryHistory
        {
            get
            {
                return _ProviderTelemetryHistory;
            }
        }

        /// <summary>
        /// Telemetry persistence service.
        /// </summary>
        public static ClientTelemetryPersistenceService? TelemetryService
        {
            get
            {
                return _TelemetryService;
            }
        }

        /// <summary>
        /// Provider telemetry persistence service.
        /// </summary>
        public static ClientProviderTelemetryPersistenceService? ProviderTelemetryService
        {
            get
            {
                return _ProviderTelemetryService;
            }
        }

        /// <summary>
        /// Start the local dashboard runtime when enabled.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task StartAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            if (!settings.LocalDashboard.Enabled && !settings.Telemetry.Enabled)
            {
                ClientConnectionState.MarkLocalDashboardDisabled(settings);
                return;
            }

            try
            {
                if (settings.LocalDashboard.Enabled) ClientConnectionState.MarkLocalDashboardStarting(settings);
                LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
                await driver.InitializeAsync(token).ConfigureAwait(false);
                ILocalRequestHistoryMethods history = driver.RequestHistory;
                IClientTelemetryHistoryMethods telemetryHistory = driver.TelemetryHistory;
                IClientProviderTelemetryHistoryMethods providerTelemetryHistory = driver.ProviderTelemetryHistory;
                await SnapshotConfigurationAsync(history, settings, token).ConfigureAwait(false);
                _Driver = driver;
                _History = history;
                _TelemetryHistory = telemetryHistory;
                _ProviderTelemetryHistory = providerTelemetryHistory;
                ClientTelemetryCommandRunner commandRunner = new ClientTelemetryCommandRunner();
                IClientTelemetryService telemetry = new ClientTelemetryService(new ClientSystemTelemetryProvider(commandRunner), new ClientGpuTelemetryProvider(commandRunner));
                ClientTelemetryPersistenceService telemetryService = new ClientTelemetryPersistenceService(settings, telemetryHistory, telemetry);
                telemetryService.Start();
                _TelemetryService = telemetryService;
                ClientProviderTelemetryPersistenceService providerTelemetryService = new ClientProviderTelemetryPersistenceService(settings, providerTelemetryHistory, new ClientProviderTelemetryService(new PrometheusMetricsParser()));
                providerTelemetryService.Start();
                _ProviderTelemetryService = providerTelemetryService;
                if (settings.LocalDashboard.Enabled)
                {
                    Xeno.Client.Chat.SqliteChatStore chatStore = new Xeno.Client.Chat.SqliteChatStore(settings.LocalDashboard.Database);
                    await chatStore.InitializeAsync(token).ConfigureAwait(false);
                    Xeno.Client.LocalInference.RoutedChatTransport chatTransport = new Xeno.Client.LocalInference.RoutedChatTransport(() => settings);
                    _ChatTransport = chatTransport;
                    Xeno.Core.Chat.ChatEngine? chatEngine = null;
                    Xeno.Client.Services.XenoWorkspace toolWorkspace = new Xeno.Client.Services.XenoWorkspace();
                    _ToolWorkspace = toolWorkspace;
                    Xeno.Client.Tools.XenoToolCatalog tools = new Xeno.Client.Tools.XenoToolCatalog(toolWorkspace, cancel => Task.FromResult<Xeno.Core.Chat.IChatStore>(chatStore), new Xeno.Client.Mcp.EngineChatGateway(chatStore, () => chatEngine!, () => settings));
                    Tools = tools;
                    Xeno.Client.Tools.Workspace.Processes.BackgroundProcessRegistry processes = new Xeno.Client.Tools.Workspace.Processes.BackgroundProcessRegistry();
                    _Processes = processes;
                    Xeno.Client.Tools.Mcp.McpClientManager mcp = new Xeno.Client.Tools.Mcp.McpClientManager(() => settings);
                    _Mcp = mcp;
                    McpTools = mcp;
                    Xeno.Client.Tools.Workspace.Planning.TaskPlanStore plans = new Xeno.Client.Tools.Workspace.Planning.TaskPlanStore();
                    _Plans = plans;
                    IReadOnlyList<Xeno.Client.Tools.Workspace.IWorkspaceTool> workspaceTools = Xeno.Client.Tools.Workspace.WorkspaceTools.Create(processes, plans, System.IO.Path.Combine(ClientSettings.SettingsDirectory, "memory"));
                    Xeno.Client.Tools.XenoChatToolHost chatTools = new Xeno.Client.Tools.XenoChatToolHost(tools, () => settings, workspaceTools, mcp);
                    // Connect configured MCP servers in the background; their tools appear once connected.
                    _ = Task.Run(() => mcp.RefreshAsync(CancellationToken.None));
                    ChatTools = chatTools;
                    chatEngine = new Xeno.Core.Chat.ChatEngine(chatStore, chatTransport, () => settings.Chat, chatTools);
                    LocalDashboardService service = new LocalDashboardService(settings, history, telemetryHistory, telemetryService, providerTelemetryHistory, providerTelemetryService, chatStore, chatEngine);
                    await service.StartAsync(token).ConfigureAwait(false);
                    _Service = service;
                    _IngestCancel = new CancellationTokenSource();
                    CancellationToken ingestToken = _IngestCancel.Token;
                    _ = Task.Run(() => IngestLocalInferenceLoopAsync(service, ingestToken));
                    PublishLocalApi(service);
                    ClientConnectionState.MarkLocalDashboardListening(settings, service.DashboardUrl);
                    Console.WriteLine("Local dashboard listening at " + service.DashboardUrl + ".");
                    if (String.Equals(settings.LocalDashboard.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Local dashboard is bound to 0.0.0.0 and may be reachable from other machines depending on firewall and network policy.");
                    }
                }
                else
                {
                    ClientConnectionState.MarkLocalDashboardDisabled(settings);
                }
            }
            catch (Exception ex)
            {
                ClientConnectionState.MarkLocalDashboardError(settings, ex.Message);
                Console.Error.WriteLine("Local dashboard failed to start: " + ex.Message);
            }
        }

        private static void PublishLocalApi(LocalDashboardService service)
        {
            try
            {
                Xeno.Client.Services.LocalApiEndpoint.Publish(new Xeno.Client.Services.LocalApiEndpoint
                {
                    Url = service.DashboardUrl,
                    Token = service.ApiToken,
                    ProcessId = Environment.ProcessId,
                    StartedUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Could not publish the local API endpoint for the CLI, desktop, and terminal apps: " + ex.Message);
            }
        }

        /// <summary>
        /// Drop what chat tools keep for a deleted conversation: its task plan, and its background processes (stopped).
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        public static void ForgetConversation(string conversationId)
        {
            if (String.IsNullOrWhiteSpace(conversationId)) return;
            _Plans?.Remove(conversationId);
            _Processes?.RemoveConversation(conversationId);
        }

        /// <summary>
        /// Stop the local dashboard runtime.
        /// </summary>
        public static void Stop()
        {
            ClientTelemetryPersistenceService? telemetryService = _TelemetryService;
            if (telemetryService != null)
            {
                telemetryService.Dispose();
                _TelemetryService = null;
                Console.WriteLine("Telemetry collection stopped.");
            }

            ClientProviderTelemetryPersistenceService? providerTelemetryService = _ProviderTelemetryService;
            if (providerTelemetryService != null)
            {
                providerTelemetryService.Dispose();
                _ProviderTelemetryService = null;
                Console.WriteLine("Provider telemetry collection stopped.");
            }

            LocalDashboardService? service = _Service;
            if (service != null)
            {
                _IngestCancel?.Cancel();
                _IngestCancel?.Dispose();
                _IngestCancel = null;
                Xeno.Client.Services.LocalApiEndpoint.Withdraw(Environment.ProcessId);
                service.Dispose();
                _Service = null;
                _ChatTransport?.Dispose();
                _ChatTransport = null;
                Tools = null;
                ChatTools = null;
                McpTools = null;
                _Mcp?.Dispose();
                _Mcp = null;
                _Processes?.Dispose();
                _Processes = null;
                _Plans = null;
                _ToolWorkspace?.Dispose();
                _ToolWorkspace = null;
                Console.WriteLine("Local dashboard stopped.");
            }

            _History = null;
            _TelemetryHistory = null;
            _ProviderTelemetryHistory = null;
            LocalDashboardDatabaseDriverBase? driver = _Driver;
            if (driver != null)
            {
                driver.Dispose();
                _Driver = null;
            }

            _InFlight.Clear();
        }

        /// <summary>
        /// Snapshot provider and model configuration.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task SnapshotConfigurationAsync(ClientSettings settings, CancellationToken token)
        {
            ILocalRequestHistoryMethods? history = _History;
            if (history == null) return;
            await SnapshotConfigurationAsync(history, settings, token).ConfigureAwait(false);
        }

        internal static async Task RefreshSettingsAsync(ClientSettings settings, CancellationToken token)
        {
            LocalDashboardService? service = _Service;
            if (service != null)
            {
                await service.RefreshSettingsAsync(settings, token).ConfigureAwait(false);
                return;
            }

            await SnapshotConfigurationAsync(settings, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Snapshot provider and model configuration to a specific history store.
        /// </summary>
        /// <param name="history">History store.</param>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task SnapshotConfigurationAsync(ILocalRequestHistoryMethods history, ClientSettings settings, CancellationToken token)
        {
            if (history is ILocalProviderSnapshotMethods providerSnapshots)
            {
                await providerSnapshots.ReplaceProviderSnapshotsAsync(settings.Runners, token).ConfigureAwait(false);
            }

            if (history is ILocalModelSnapshotMethods modelSnapshots)
            {
                await modelSnapshots.ReplaceModelSnapshotsAsync(settings.Runners, token).ConfigureAwait(false);
            }

            if (history is ILocalDashboardMetadataMethods metadata)
            {
                await metadata.SetMetadataAsync("last_configuration_snapshot_utc", DateTime.UtcNow.ToString("O"), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Record a received server request.
        /// </summary>
        /// <param name="request">Server request.</param>
        /// <param name="runner">Selected provider endpoint.</param>
        /// <param name="requestId">Request ID.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Request path.</param>
        /// <param name="requestorIp">Requestor IP.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task RecordReceivedAsync(ServerRequestMessage request, ClientRunnerSettings? runner, string requestId, string method, string path, string requestorIp, CancellationToken token)
        {
            ILocalRequestHistoryMethods? history = _History;
            if (history == null) return;
            token.ThrowIfCancellationRequested();
            LocalRequestHistoryEntry entry = new LocalRequestHistoryEntry
            {
                Id = requestId,
                TenantId = request.TenantId,
                RequestorTenantId = request.RequestorTenantId,
                RequestorPrincipalType = request.RequestorPrincipalType,
                RequestorPrincipalId = request.RequestorPrincipalId,
                ClientId = request.ClientId,
                ReceivedUtc = DateTime.UtcNow,
                Method = method,
                Path = path,
                RequestorIp = requestorIp,
                ProviderName = runner?.Name,
                ApiType = runner?.ApiType ?? request.ApiType,
                ModelName = request.Model,
                Outcome = LocalRequestOutcomeEnum.Processing,
                Streamed = request.Stream,
                RequestHeaders = LocalSecretRedactor.RedactHeaders(request.Headers),
                RequestBody = request.Body
            };
            if (runner != null && _ProviderTelemetryHistory != null)
            {
                ClientProviderTelemetrySampleDetail? providerTelemetry = await _ProviderTelemetryHistory.ReadLatestAsync(runner.Name, token).ConfigureAwait(false);
                if (providerTelemetry != null)
                {
                    entry.ProviderTelemetrySampleId = providerTelemetry.Sample.Id;
                    entry.ProviderTelemetryHealthStatus = providerTelemetry.Sample.HealthStatus.ToString();
                    entry.ProviderTelemetryQueuedRequests = providerTelemetry.Sample.QueuedRequests;
                    entry.Timeline.Add(new LocalRequestTimelineEvent { Stage = LocalRequestTimelineStageEnum.ProviderSelected, TimestampUtc = DateTime.UtcNow, Message = "Provider telemetry sample linked: " + providerTelemetry.Sample.Id });
                }
            }

            entry.Timeline.Add(new LocalRequestTimelineEvent { Stage = LocalRequestTimelineStageEnum.Received, TimestampUtc = entry.ReceivedUtc, Message = "Request received from Xeno server." });
            if (runner != null) entry.Timeline.Add(new LocalRequestTimelineEvent { Stage = LocalRequestTimelineStageEnum.ProviderSelected, TimestampUtc = DateTime.UtcNow, Message = "Provider selected: " + runner.Name });
            _InFlight[requestId] = entry;
            await history.UpsertAsync(entry, ClientSettings.Load().LocalDashboard.HistoryRetention, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Record a completed server request.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <param name="response">Client response.</param>
        /// <param name="streamed">Whether streamed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task RecordCompletedAsync(string requestId, ClientResponseMessage response, bool streamed, CancellationToken token)
        {
            ILocalRequestHistoryMethods? history = _History;
            if (history == null) return;
            token.ThrowIfCancellationRequested();
            LocalRequestHistoryEntry entry;
            if (!_InFlight.TryGetValue(requestId, out entry!))
            {
                entry = await history.GetAsync(requestId, token).ConfigureAwait(false) ?? new LocalRequestHistoryEntry { Id = requestId, ReceivedUtc = DateTime.UtcNow };
            }

            entry.CompletedUtc = DateTime.UtcNow;
            entry.StatusCode = response.StatusCode;
            entry.Outcome = response.Success ? LocalRequestOutcomeEnum.Success : LocalRequestOutcomeEnum.Failure;
            entry.Error = response.Error;
            entry.Streamed = streamed;
            entry.ResponseHeaders = LocalSecretRedactor.RedactHeaders(response.Headers);
            entry.ResponseBody = response.Body;
            ApplyTelemetry(entry, response.Telemetry);
            entry.Timeline.Add(new LocalRequestTimelineEvent { Stage = response.Success ? LocalRequestTimelineStageEnum.Completed : LocalRequestTimelineStageEnum.Failed, TimestampUtc = entry.CompletedUtc.Value, Message = response.Success ? "Request completed." : response.Error });
            await history.UpsertAsync(entry, ClientSettings.Load().LocalDashboard.HistoryRetention, token).ConfigureAwait(false);
            _InFlight.TryRemove(requestId, out LocalRequestHistoryEntry? _);
        }

        /// <summary>
        /// Record a canceled server request.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <param name="message">Cancellation message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task RecordCanceledAsync(string requestId, string message, CancellationToken token)
        {
            ILocalRequestHistoryMethods? history = _History;
            if (history == null) return;
            token.ThrowIfCancellationRequested();
            LocalRequestHistoryEntry entry = await GetInflightOrStoredAsync(history, requestId, token).ConfigureAwait(false);
            entry.CompletedUtc = DateTime.UtcNow;
            entry.Outcome = LocalRequestOutcomeEnum.Failure;
            entry.Error = message;
            entry.Timeline.Add(new LocalRequestTimelineEvent { Stage = LocalRequestTimelineStageEnum.Failed, TimestampUtc = entry.CompletedUtc.Value, Message = message });
            await history.UpsertAsync(entry, ClientSettings.Load().LocalDashboard.HistoryRetention, token).ConfigureAwait(false);
            _InFlight.TryRemove(requestId, out LocalRequestHistoryEntry? _);
        }

        /// <summary>
        /// Record that upstream response headers were received.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <param name="response">Stream-start response.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task RecordResponseStartedAsync(string requestId, ClientResponseMessage response, CancellationToken token)
        {
            ILocalRequestHistoryMethods? history = _History;
            if (history == null) return;
            token.ThrowIfCancellationRequested();
            LocalRequestHistoryEntry entry = await GetInflightOrStoredAsync(history, requestId, token).ConfigureAwait(false);
            entry.StatusCode = response.StatusCode;
            entry.Outcome = LocalRequestOutcomeEnum.Processing;
            entry.Streamed = true;
            entry.ResponseHeaders = LocalSecretRedactor.RedactHeaders(response.Headers);
            ApplyTelemetry(entry, response.Telemetry);
            AddTimelineEventIfMissing(entry, LocalRequestTimelineStageEnum.UpstreamHeaders, "Upstream response headers received.");
            await history.UpsertAsync(entry, ClientSettings.Load().LocalDashboard.HistoryRetention, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Record streaming chunk progress for an in-flight request.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <param name="sequence">Stream chunk sequence.</param>
        /// <param name="timeToFirstByteMs">Time to first byte in milliseconds when available.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task RecordStreamChunkAsync(string requestId, int sequence, double? timeToFirstByteMs, CancellationToken token)
        {
            ILocalRequestHistoryMethods? history = _History;
            if (history == null) return;
            token.ThrowIfCancellationRequested();
            LocalRequestHistoryEntry entry = await GetInflightOrStoredAsync(history, requestId, token).ConfigureAwait(false);
            entry.Streamed = true;
            entry.Telemetry.StreamChunkCount = Math.Max(entry.Telemetry.StreamChunkCount, sequence);
            if (timeToFirstByteMs.HasValue && !entry.Telemetry.TimeToFirstByteMs.HasValue)
            {
                entry.Telemetry.TimeToFirstByteMs = timeToFirstByteMs.Value;
                AddTimelineEventIfMissing(entry, LocalRequestTimelineStageEnum.FirstByte, "First response byte received.");
            }

            await history.UpsertAsync(entry, ClientSettings.Load().LocalDashboard.HistoryRetention, token).ConfigureAwait(false);
        }

        private static async Task<LocalRequestHistoryEntry> GetInflightOrStoredAsync(ILocalRequestHistoryMethods history, string requestId, CancellationToken token)
        {
            LocalRequestHistoryEntry entry;
            if (!_InFlight.TryGetValue(requestId, out entry!))
            {
                entry = await history.GetAsync(requestId, token).ConfigureAwait(false) ?? new LocalRequestHistoryEntry { Id = requestId, ReceivedUtc = DateTime.UtcNow };
                _InFlight[requestId] = entry;
            }

            return entry;
        }

        private static void AddTimelineEventIfMissing(LocalRequestHistoryEntry entry, LocalRequestTimelineStageEnum stage, string message)
        {
            foreach (LocalRequestTimelineEvent item in entry.Timeline)
            {
                if (item.Stage == stage) return;
            }

            entry.Timeline.Add(new LocalRequestTimelineEvent { Stage = stage, TimestampUtc = DateTime.UtcNow, Message = message });
        }

        private static void ApplyTelemetry(LocalRequestHistoryEntry entry, RequestHistoryTelemetry? telemetry)
        {
            if (telemetry == null) return;
            entry.Telemetry.DurationMs = telemetry.UpstreamTotalMs ?? telemetry.TotalTimeMs;
            entry.Telemetry.TimeToHeadersMs = telemetry.RequestToHeadersMs;
            entry.Telemetry.TimeToFirstByteMs = telemetry.TimeToFirstTokenMs;
            entry.Telemetry.TotalTokens = telemetry.TotalTokens;
            entry.Telemetry.PromptTokens = telemetry.PromptTokens;
            entry.Telemetry.CompletionTokens = telemetry.CompletionTokens;
            entry.Telemetry.StreamChunkCount = telemetry.StreamChunkCount ?? 0;
            entry.Telemetry.StreamEventCount = telemetry.StreamEventCount ?? 0;
        }

        private static async Task IngestLocalInferenceLoopAsync(LocalDashboardService service, CancellationToken token)
        {
            // Copies local endpoint requests from the listener's JSON log into the database every 15 seconds, so the
            // Usage page keeps more history than the log's last 1000 requests.
            using (PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(15)))
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await service.IngestLocalInferenceRequestsAsync(token).ConfigureAwait(false);
                        await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException || ex is System.IO.IOException || ex is InvalidOperationException || ex is ObjectDisposedException)
                    {
                        Console.WriteLine("Local endpoint usage ingest failed: " + ex.Message);
                        try
                        {
                            await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
            }
        }
    }
}

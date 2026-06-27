namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Core.Models;

    /// <summary>
    /// Local dashboard runtime bridge for the Xeno client worker.
    /// </summary>
    public static class LocalDashboardRuntime
    {
        private static readonly ConcurrentDictionary<string, LocalRequestHistoryEntry> _InFlight = new ConcurrentDictionary<string, LocalRequestHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        private static LocalDashboardDatabaseDriverBase? _Driver;
        private static ILocalRequestHistoryMethods? _History;
        private static LocalDashboardService? _Service;

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
        /// Start the local dashboard runtime when enabled.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task StartAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            if (!settings.LocalDashboard.Enabled)
            {
                ClientConnectionState.MarkLocalDashboardDisabled(settings);
                return;
            }

            try
            {
                ClientConnectionState.MarkLocalDashboardStarting(settings);
                LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
                await driver.InitializeAsync(token).ConfigureAwait(false);
                ILocalRequestHistoryMethods history = driver.RequestHistory;
                await SnapshotConfigurationAsync(history, settings, token).ConfigureAwait(false);
                LocalDashboardService service = new LocalDashboardService(settings, history);
                await service.StartAsync(token).ConfigureAwait(false);
                _Driver = driver;
                _History = history;
                _Service = service;
                ClientConnectionState.MarkLocalDashboardListening(settings, service.DashboardUrl);
                Console.WriteLine("Local dashboard listening at " + service.DashboardUrl + ".");
                if (String.Equals(settings.LocalDashboard.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Local dashboard is bound to 0.0.0.0 and may be reachable from other machines depending on firewall and network policy.");
                }
            }
            catch (Exception ex)
            {
                ClientConnectionState.MarkLocalDashboardError(settings, ex.Message);
                Console.Error.WriteLine("Local dashboard failed to start: " + ex.Message);
            }
        }

        /// <summary>
        /// Stop the local dashboard runtime.
        /// </summary>
        public static void Stop()
        {
            LocalDashboardService? service = _Service;
            if (service != null)
            {
                service.Dispose();
                _Service = null;
                Console.WriteLine("Local dashboard stopped.");
            }

            _History = null;
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
    }
}

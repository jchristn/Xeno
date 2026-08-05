namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Xeno.Core.Enums;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;

    /// <summary>
    /// Base database driver.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Database provider type.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Settings { get; private set; }

        /// <summary>
        /// Platform state.
        /// </summary>
        public XenoPlatform Platform { get; protected set; } = new XenoPlatform();

        /// <summary>
        /// Server-collected host telemetry methods.
        /// </summary>
        public IClientTelemetryMethods? ClientTelemetry { get; protected set; } = null;

        /// <summary>
        /// Server-collected provider telemetry methods.
        /// </summary>
        public IClientProviderTelemetryMethods? ClientProviderTelemetry { get; protected set; } = null;

        /// <summary>
        /// Server telemetry collection attempt methods.
        /// </summary>
        public IClientTelemetryCollectionAttemptMethods? ClientTelemetryCollectionAttempts { get; protected set; } = null;

        /// <summary>
        /// Persisted request history methods. Request history is stored in its own table, never in
        /// the in-memory platform state.
        /// </summary>
        public IRequestHistoryMethods? RequestHistory { get; protected set; } = null;

        /// <summary>
        /// Persisted audit record methods. Audit records are stored in their own table, never in
        /// the in-memory platform state.
        /// </summary>
        public IAuditMethods? Audit { get; protected set; } = null;

        /// <summary>
        /// Persisted cloud onboarding request methods. Onboarding requests are stored in their own
        /// table, never in the in-memory platform state.
        /// </summary>
        public IOnboardingMethods? Onboarding { get; protected set; } = null;

        /// <summary>
        /// Logging module. May be null when no logger is supplied.
        /// </summary>
        protected LoggingModule? _Logging { get; }

        /// <summary>
        /// Log message header.
        /// </summary>
        protected string _Header = "[Database] ";

        /// <summary>
        /// Instantiate the database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Optional logging module.</param>
        protected DatabaseDriverBase(DatabaseSettings settings, LoggingModule? logging = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging;
        }

        /// <summary>
        /// Initialize the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public virtual async Task InitializeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            _Logging?.Debug(_Header + "initializing " + DatabaseType + " database");

            bool storageAvailable = await InitializeStorageAsync(token).ConfigureAwait(false);
            string? stateJson = storageAvailable ? await LoadStateJsonAsync(token).ConfigureAwait(false) : null;
            if (!String.IsNullOrWhiteSpace(stateJson))
            {
                PlatformState? state = JsonUtil.Deserialize<PlatformState>(stateJson);
                if (state != null) Platform.LoadState(state);
                if (storageAvailable) await MigrateLegacyAuditRecordsAsync(token).ConfigureAwait(false);
                if (storageAvailable) await SaveAsync(token).ConfigureAwait(false);
            }
            else
            {
                await Platform.InitializeAsync(token).ConfigureAwait(false);
                if (storageAvailable) await SaveAsync(token).ConfigureAwait(false);
            }

            _Logging?.Debug(_Header + "initialized " + DatabaseType + " database");
        }

        /// <summary>
        /// One-time migration of audit records that were previously embedded in the serialized
        /// platform state into the dedicated audit table. After this runs the state blob is saved
        /// without audit records, so this is a no-op on subsequent startups.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task MigrateLegacyAuditRecordsAsync(CancellationToken token)
        {
            if (Audit == null) return;
            List<AuditRecord> pending = Platform.DrainPendingAuditRecords();
            if (pending.Count == 0) return;

            int migrated = 0;
            foreach (AuditRecord record in pending)
            {
                try
                {
                    await Audit.InsertAsync(record, token).ConfigureAwait(false);
                    migrated++;
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(_Header + "failed to migrate audit record " + record.Id + ": " + ex.Message);
                }
            }

            _Logging?.Info(_Header + "migrated " + migrated + " legacy audit record(s) from platform state to the audit table");
        }

        /// <summary>
        /// Persist current platform state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task SaveAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            string stateJson = JsonUtil.Serialize(Platform.ToState());
            await SaveStateJsonAsync(stateJson, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Purge provider-specific tenant records that are persisted outside the serialized platform state.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        public virtual Task<int> PurgeTenantDataAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            token.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }

        /// <summary>
        /// Query rank-ordered model token usage from persisted request history.
        /// </summary>
        /// <param name="request">Leaderboard request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model token leaderboard.</returns>
        public virtual async Task<ModelTokenLeaderboardResponse> GetModelTokenLeaderboardAsync(ModelTokenLeaderboardRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();

            int limit = request.Limit.HasValue ? Math.Clamp(request.Limit.Value, 1, 1000) : 100;
            int offset = request.Offset.HasValue ? Math.Max(0, request.Offset.Value) : 0;

            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            if (RequestHistory != null)
            {
                RequestHistoryQuery query = new RequestHistoryQuery
                {
                    TenantId = String.IsNullOrWhiteSpace(request.TenantId) ? null : request.TenantId,
                    FromUtc = request.FromUtc?.ToUniversalTime(),
                    ToUtc = request.ToUtc?.ToUniversalTime(),
                    IncludeBody = true,
                    Limit = 0
                };
                RequestHistoryPage page = await RequestHistory.QueryAsync(query, token).ConfigureAwait(false);
                entries = page.Items;
            }

            Dictionary<string, ModelTokenUsage> usageByModel = new Dictionary<string, ModelTokenUsage>(StringComparer.OrdinalIgnoreCase);
            foreach (RequestHistoryEntry entry in entries)
            {
                string model = entry.Model ?? String.Empty;
                if (String.IsNullOrWhiteSpace(model)) continue;

                if (!usageByModel.TryGetValue(model, out ModelTokenUsage? usage))
                {
                    usage = new ModelTokenUsage();
                    usageByModel[model] = usage;
                }

                usage.RequestCount++;
                usage.PromptTokens += Math.Max(0, entry.Telemetry?.PromptTokens ?? 0);
                usage.OutputTokens += Math.Max(0, entry.Telemetry?.CompletionTokens ?? 0);
                int reportedTotalTokens = Math.Max(0, entry.Telemetry?.TotalTokens ?? 0);
                usage.TotalTokens += reportedTotalTokens > 0
                    ? reportedTotalTokens
                    : Math.Max(0, entry.Telemetry?.PromptTokens ?? 0) + Math.Max(0, entry.Telemetry?.CompletionTokens ?? 0);
            }

            List<KeyValuePair<string, ModelTokenUsage>> ranked = usageByModel
                .OrderByDescending(item => item.Value.TotalTokens)
                .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, ModelTokenUsage> models = new Dictionary<string, ModelTokenUsage>();
            foreach (KeyValuePair<string, ModelTokenUsage> item in ranked.Skip(offset).Take(limit))
            {
                models[item.Key] = item.Value;
            }

            return new ModelTokenLeaderboardResponse
            {
                Models = models,
                Total = ranked.Count,
                Limit = limit,
                Offset = offset
            };
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources asynchronously.
        /// </summary>
        /// <returns>Value task.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Initialize provider storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if storage is available.</returns>
        protected abstract Task<bool> InitializeStorageAsync(CancellationToken token = default);

        /// <summary>
        /// Load serialized platform state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Serialized state, if available.</returns>
        protected abstract Task<string?> LoadStateJsonAsync(CancellationToken token = default);

        /// <summary>
        /// Save serialized platform state.
        /// </summary>
        /// <param name="stateJson">Serialized state.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        protected abstract Task SaveStateJsonAsync(string stateJson, CancellationToken token = default);
    }
}

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
        /// Tenant methods (normalized relational store).
        /// </summary>
        public ITenantMethods? Tenants { get; protected set; } = null;

        /// <summary>
        /// User methods (normalized relational store).
        /// </summary>
        public IUserMethods? Users { get; protected set; } = null;

        /// <summary>
        /// Credential methods (normalized relational store).
        /// </summary>
        public ICredentialMethods? Credentials { get; protected set; } = null;

        /// <summary>
        /// Authentication session methods (normalized relational store).
        /// </summary>
        public IAuthSessionMethods? AuthSessions { get; protected set; } = null;

        /// <summary>
        /// Role methods (normalized relational store).
        /// </summary>
        public IRoleMethods? Roles { get; protected set; } = null;

        /// <summary>
        /// Permission methods (normalized relational store).
        /// </summary>
        public IPermissionMethods? Permissions { get; protected set; } = null;

        /// <summary>
        /// Role assignment methods (normalized relational store).
        /// </summary>
        public IRoleAssignmentMethods? RoleAssignments { get; protected set; } = null;

        /// <summary>
        /// Client methods (normalized relational store).
        /// </summary>
        public IClientMethods? Clients { get; protected set; } = null;

        /// <summary>
        /// Model definition methods (normalized relational store).
        /// </summary>
        public IModelDefinitionMethods? ModelDefinitions { get; protected set; } = null;

        /// <summary>
        /// Model configuration methods (normalized relational store).
        /// </summary>
        public IModelConfigurationMethods? ModelConfigurations { get; protected set; } = null;

        /// <summary>
        /// Routing policy methods (normalized relational store).
        /// </summary>
        public IRoutingPolicyMethods? RoutingPolicies { get; protected set; } = null;

        /// <summary>
        /// Ledger tenant link methods (normalized relational store).
        /// </summary>
        public ILedgerTenantLinkMethods? LedgerTenantLinks { get; protected set; } = null;

        /// <summary>
        /// Ledger account link methods (normalized relational store).
        /// </summary>
        public ILedgerAccountLinkMethods? LedgerAccountLinks { get; protected set; } = null;

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
            _Logging?.Debug(_Header + "initializing " + DatabaseType + " database schema");

            bool storageAvailable = await InitializeStorageAsync(token).ConfigureAwait(false);
            if (storageAvailable)
            {
                // The control plane is stored in normalized relational tables. On first boot, build the
                // default control-plane graph in memory and persist it through the repositories; on
                // subsequent boots this is a no-op because the tenant table is already populated.
                XenoPlatform seed = new XenoPlatform();
                await seed.InitializeAsync(token).ConfigureAwait(false);
                await DatabaseSeeder.SeedIfEmptyAsync(this, seed, token).ConfigureAwait(false);
            }

            _Logging?.Debug(_Header + DatabaseType + " database ready");
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
    }
}

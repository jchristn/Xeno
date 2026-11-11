namespace Xeno.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using Xeno.Core.Analytics;
    using Xeno.Core.Chat;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Exceptions;
    using Xeno.Core.Models;
    using Xeno.Core.Models.Telemetry;
    using Xeno.Core.Settings;

    /// <summary>
    /// Usage analytics routes: network request usage, chat usage, and the provider health strip. Every chart on every
    /// Xeno surface draws these responses; none computes them itself.
    /// </summary>
    public sealed partial class XenoServer
    {
        #region Private-Members

        private volatile bool _UsageBackfillPending = true;
        private const int ConnectedClientRetentionDays = 30;
        private static readonly TimeSpan ConnectedClientSampleInterval = TimeSpan.FromMinutes(1);
        private CancellationTokenSource? _ConnectedClientSampleCancellation = null;
        private DateTime _ConnectedClientNextPruneUtc = DateTime.MinValue;
        private const int UsageBackfillBatchSize = 5000;

        #endregion

        #region Internal-Methods

        /// <summary>
        /// GET /v1.0/api/request-history/analytics/usage.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="requestContext">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task HandleUsageAnalyticsAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            await RequirePermissionAsync(requestContext, "RequestHistory", OperationTypeEnum.Read, token).ConfigureAwait(false);
            UsageAnalyticsQuery query = UsageQueryParser.Parse(name => UsageQueryValue(context, name));
            string? tenantId = await UsageTenantScopeAsync(requestContext, query, token).ConfigureAwait(false);
            AnalyticsSettings settings = _Settings.Analytics ?? new AnalyticsSettings();
            UsageAnalytics analytics;
            if (_Database?.RequestHistory == null || !_Settings.RequestHistory.Enabled)
            {
                analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.NetworkRequests, new List<UsageRecord>(), query, settings.ToOptions());
            }
            else
            {
                RequestHistoryUsageFilter filter = new RequestHistoryUsageFilter
                {
                    TenantId = tenantId,
                    FromUtc = query.FromUtc,
                    ToUtc = query.ToUtc,
                    Model = query.Model,
                    ClientId = query.ClientId,
                    UserId = query.UserId,
                    CredentialId = query.CredentialId
                };
                analytics = await UsageAnalyticsRunner.RunAsync(UsageDatasetEnum.NetworkRequests, _Database.RequestHistory.EnumerateUsageAsync(filter, token), query, settings.ToOptions(), token).ConfigureAwait(false);
                if (_UsageBackfillPending)
                {
                    analytics.Truncated = true;
                    analytics.TruncatedReason = "Older requests are still being indexed for usage analytics; the numbers will fill in shortly.";
                }
            }

            await SendJsonAsync(context, analytics).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /v1.0/api/client-telemetry/connected-history.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="requestContext">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task HandleConnectedClientHistoryAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
            ProviderHealthQuery query = UsageQueryParser.ParseHealth(name => UsageQueryValue(context, name));
            List<ConnectedClientSample> samples = _Database?.ConnectedClientSamples == null
                ? new List<ConnectedClientSample>()
                : await _Database.ConnectedClientSamples.ListAsync(requestContext.TenantId, query.FromUtc, query.ToUtc, token).ConfigureAwait(false);
            await SendJsonAsync(context, ConnectedClientHistoryBuilder.Build(samples, query.FromUtc, query.ToUtc, query.BucketMinutes)).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /v1.0/api/client-telemetry/providers/health.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="requestContext">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task HandleProviderHealthAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            await RequirePermissionAsync(requestContext, "ClientTelemetry", OperationTypeEnum.Read, token).ConfigureAwait(false);
            ProviderHealthQuery query = UsageQueryParser.ParseHealth(name => UsageQueryValue(context, name));
            List<ProviderHealthSample> samples = new List<ProviderHealthSample>();
            if (_Database?.ClientProviderTelemetry != null)
            {
                int offset = 0;
                const int page = 1000;
                while (offset < 50000)
                {
                    ServerProviderTelemetrySearchResult result = await _Database.ClientProviderTelemetry.SearchProviderAsync(new ServerProviderTelemetrySearchRequest
                    {
                        TenantId = requestContext.TenantId,
                        ClientId = query.ClientId,
                        ProviderName = query.ProviderName,
                        FromUtc = query.FromUtc,
                        ToUtc = query.ToUtc,
                        Limit = page,
                        Offset = offset
                    }, token).ConfigureAwait(false);
                    foreach (ServerProviderTelemetrySampleRecord item in result.Items)
                    {
                        if (item.HealthStatus == ClientProviderTelemetryHealthStatusEnum.Unknown) continue;
                        samples.Add(new ProviderHealthSample { TimestampUtc = item.CollectedUtc, ProviderName = item.ProviderName, ClientId = item.ClientId, Healthy = item.HealthStatus == ClientProviderTelemetryHealthStatusEnum.Healthy });
                    }

                    if (result.Items.Count < page) break;
                    offset += page;
                }
            }

            await SendJsonAsync(context, ProviderHealthBuilder.Build(samples, query)).ConfigureAwait(false);
        }

        /// <summary>
        /// Chat usage analytics for the caller, or the tenant with scope=tenant.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="requestContext">Request context.</param>
        /// <param name="store">Chat store already scoped and authorized.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task HandleChatUsageAnalyticsAsync(HttpContextBase context, RequestContext requestContext, IChatStore store, CancellationToken token)
        {
            UsageAnalyticsQuery query = UsageQueryParser.Parse(name => UsageQueryValue(context, name));
            if (query.GroupBy == UsageGroupByEnum.Tenant || query.GroupBy == UsageGroupByEnum.Client || query.GroupBy == UsageGroupByEnum.Credential) throw new ArgumentException("Chat usage groups by model, provider, route, or user.");
            if (query.GroupBy == UsageGroupByEnum.User && !String.Equals(UsageQueryValue(context, "scope"), "tenant", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Grouping chat usage by user needs scope=tenant.");
            query.TenantId = null;
            AnalyticsSettings settings = _Settings.Analytics ?? new AnalyticsSettings();
            UsageAnalytics analytics = await UsageAnalyticsRunner.RunAsync(UsageDatasetEnum.Chat, store.EnumerateUsageAsync(query.FromUtc, query.ToUtc, token), query, settings.ToOptions(), token).ConfigureAwait(false);
            await SendJsonAsync(context, analytics).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task<string?> UsageTenantScopeAsync(RequestContext requestContext, UsageAnalyticsQuery query, CancellationToken token)
        {
            bool globalAdmin = await _Authorization.IsSystemAdminAsync(_Database!, requestContext, token).ConfigureAwait(false);
            string tenant = requestContext.TenantId;
            if (query.TenantId != null && !String.Equals(query.TenantId, tenant, StringComparison.Ordinal))
            {
                if (!globalAdmin) throw new AuthorizationDeniedException(403, "GlobalAdminRequired", "Only a global administrator can read another tenant's usage.");
                tenant = query.TenantId;
            }

            if (query.GroupBy == UsageGroupByEnum.Tenant)
            {
                if (!globalAdmin) throw new AuthorizationDeniedException(403, "GlobalAdminRequired", "Only a global administrator can group usage by tenant.");
                return query.TenantId;
            }

            if (query.GroupBy == UsageGroupByEnum.User || query.GroupBy == UsageGroupByEnum.Credential || query.UserId != null || query.CredentialId != null)
            {
                bool tenantAdmin = globalAdmin || await _Authorization.IsTenantAdminAsync(_Database!, requestContext, tenant, token).ConfigureAwait(false);
                if (!tenantAdmin) throw new AuthorizationDeniedException(403, "TenantAdminRequired", "Only a tenant administrator can see usage by user or credential.");
            }

            return tenant;
        }

        private static string? UsageQueryValue(HttpContextBase context, string name)
        {
            string value = DecodeQueryValue(context.Request.Query.Elements.Get(name));
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }

        private void StartConnectedClientSampling()
        {
            _ConnectedClientSampleCancellation = new CancellationTokenSource();
            CancellationToken token = _ConnectedClientSampleCancellation.Token;
            _ = Task.Run(async () => await ConnectedClientSampleLoopAsync(token).ConfigureAwait(false));
        }

        private void StopConnectedClientSampling()
        {
            _ConnectedClientSampleCancellation?.Cancel();
            _ConnectedClientSampleCancellation?.Dispose();
            _ConnectedClientSampleCancellation = null;
        }

        private async Task ConnectedClientSampleLoopAsync(CancellationToken token)
        {
            try
            {
                using (PeriodicTimer timer = new PeriodicTimer(ConnectedClientSampleInterval))
                {
                    do
                    {
                        try
                        {
                            await SampleConnectedClientsAsync(token).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _Logging.Warn("[XenoServer] connected client sample failed: " + ex.Message);
                        }
                    }
                    while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        private async Task SampleConnectedClientsAsync(CancellationToken token)
        {
            if (_Database?.ConnectedClientSamples == null || _Database.Tenants == null || _Database.Clients == null) return;
            DateTime now = DateTime.UtcNow;
            foreach (Tenant tenant in await _Database.Tenants.EnumerateAsync(token).ConfigureAwait(false))
            {
                List<XenoClientRecord> clients = await _Database.Clients.EnumerateAsync(tenant.Id, token).ConfigureAwait(false);
                await _Database.ConnectedClientSamples.InsertAsync(new ConnectedClientSample { TenantId = tenant.Id, SampledUtc = now, Connected = clients.Count(item => item.Connected), Total = clients.Count }, token).ConfigureAwait(false);
            }

            if (now < _ConnectedClientNextPruneUtc) return;
            _ConnectedClientNextPruneUtc = now.AddHours(1);
            await _Database.ConnectedClientSamples.DeleteOlderThanAsync(now.AddDays(-ConnectedClientRetentionDays), token).ConfigureAwait(false);
        }

        private async Task BackfillUsageColumnsAsync(CancellationToken token)
        {
            if (_Database?.RequestHistory == null)
            {
                _UsageBackfillPending = false;
                return;
            }

            try
            {
                long total = 0;
                while (!token.IsCancellationRequested)
                {
                    int filled = await _Database.RequestHistory.BackfillUsageAsync(UsageBackfillBatchSize, token).ConfigureAwait(false);
                    total += filled;
                    if (filled == 0) break;
                }

                if (total > 0) _Logging.Info(_RequestHistoryRetentionHeader + "indexed " + total + " request history entries for usage analytics");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_RequestHistoryRetentionHeader + "usage analytics backfill failed: " + ex.Message);
            }

            _UsageBackfillPending = false;
        }

        #endregion
    }
}

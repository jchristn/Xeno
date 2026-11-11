namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using Xeno.Client.Routing;
    using Xeno.Core.Analytics;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Usage analytics routes of the local dashboard API: served requests, local endpoint requests, chat, and the
    /// provider health strip. The desktop app, terminal app, CLI, MCP, and web dashboard all read these.
    /// </summary>
    public sealed partial class LocalDashboardService
    {
        #region Private-Members

        private static readonly SemaphoreSlim _IngestLock = new SemaphoreSlim(1, 1);
        private DateTime _LastLocalInferencePruneUtc = DateTime.MinValue;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Copy new local endpoint request records from the listener log into the local dashboard database, and prune
        /// records older than the retention setting at most once an hour. Safe to call often and concurrently.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Records added.</returns>
        public async Task<int> IngestLocalInferenceRequestsAsync(CancellationToken token)
        {
            await _IngestLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<LocalInferenceRequestRecord> records = LocalInferenceRequestLog.Read(0, 5000);
                int added = records.Count == 0 ? 0 : await _History.IngestLocalInferenceAsync(records, token).ConfigureAwait(false);
                DateTime now = DateTime.UtcNow;
                if (now - _LastLocalInferencePruneUtc > TimeSpan.FromHours(1))
                {
                    _LastLocalInferencePruneUtc = now;
                    await _History.PruneLocalInferenceAsync(now.AddDays(-_Settings.LocalInferenceServer.RequestRetentionDays), token).ConfigureAwait(false);
                }

                return added;
            }
            finally
            {
                _IngestLock.Release();
            }
        }

        #endregion

        #region Private-Methods

        private async Task<bool> TryHandleUsageAsync(HttpContextBase context, string method, string path, CancellationToken token)
        {
            if (method != "GET") return false;
            bool served = String.Equals(path, "/api/v1/requests/analytics/usage", StringComparison.OrdinalIgnoreCase);
            bool endpoint = String.Equals(path, "/api/v1/local-inference/analytics/usage", StringComparison.OrdinalIgnoreCase);
            bool chat = String.Equals(path, "/api/v1/chat/analytics/usage", StringComparison.OrdinalIgnoreCase);
            bool health = String.Equals(path, "/api/v1/telemetry/providers/health", StringComparison.OrdinalIgnoreCase);
            if (!served && !endpoint && !chat && !health) return false;

            if (health)
            {
                ProviderHealthQuery healthQuery = UsageQueryParser.ParseHealth(name => QueryValue(context, name));
                List<ProviderHealthSample> samples = new List<ProviderHealthSample>();
                if (_ProviderTelemetryHistory != null)
                {
                    int offset = 0;
                    const int page = 1000;
                    while (offset < 50000)
                    {
                        ClientProviderTelemetrySearchResult result = await _ProviderTelemetryHistory.SearchAsync(new ClientProviderTelemetrySearchRequest { FromUtc = healthQuery.FromUtc, ToUtc = healthQuery.ToUtc, ProviderName = healthQuery.ProviderName, Limit = page, Offset = offset }, token).ConfigureAwait(false);
                        foreach (ClientProviderTelemetrySampleRecord item in result.Samples)
                        {
                            if (item.HealthStatus == ClientProviderTelemetryHealthStatusEnum.Unknown) continue;
                            samples.Add(new ProviderHealthSample { TimestampUtc = item.CollectedUtc, ProviderName = item.ProviderName, Healthy = item.HealthStatus == ClientProviderTelemetryHealthStatusEnum.Healthy });
                        }

                        if (result.Samples.Count < page) break;
                        offset += page;
                    }
                }

                await SendJsonAsync(context, ProviderHealthBuilder.Build(samples, healthQuery)).ConfigureAwait(false);
                return true;
            }

            UsageAnalyticsQuery query = UsageQueryParser.Parse(name => QueryValue(context, name));
            if (query.GroupBy == UsageGroupByEnum.Client || query.GroupBy == UsageGroupByEnum.Tenant || query.GroupBy == UsageGroupByEnum.User || query.GroupBy == UsageGroupByEnum.Credential)
            {
                throw new ArgumentException("This machine's usage groups by model, provider, or route.");
            }

            UsageAnalyticsOptions options = (_Settings.Analytics ?? new ClientAnalyticsSettings()).ToOptions();
            UsageAnalytics analytics;
            if (served)
            {
                analytics = await UsageAnalyticsRunner.RunAsync(UsageDatasetEnum.ServedRequests, _History.EnumerateServedUsageAsync(query.FromUtc, query.ToUtc, token), query, options, token).ConfigureAwait(false);
            }
            else if (endpoint)
            {
                await IngestLocalInferenceRequestsAsync(token).ConfigureAwait(false);
                analytics = await UsageAnalyticsRunner.RunAsync(UsageDatasetEnum.LocalEndpoint, _History.EnumerateLocalInferenceUsageAsync(query.FromUtc, query.ToUtc, token), query, options, token).ConfigureAwait(false);
            }
            else
            {
                if (_Chat == null) throw new InvalidOperationException("Chat is not available in this client.");
                analytics = await UsageAnalyticsRunner.RunAsync(UsageDatasetEnum.Chat, _Chat.EnumerateUsageAsync(query.FromUtc, query.ToUtc, token), query, options, token).ConfigureAwait(false);
            }

            await SendJsonAsync(context, analytics).ConfigureAwait(false);
            return true;
        }

        private static string? QueryValue(HttpContextBase context, string name)
        {
            string? value = context.Request.Query.Elements.Get(name);
            if (String.IsNullOrWhiteSpace(value)) return null;
            return Uri.UnescapeDataString(value);
        }

        #endregion
    }
}

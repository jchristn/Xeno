namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Telemetry;

    /// <summary>
    /// Runs a usage analytics query with instrumentation: an <c>analytics usage</c> span and the
    /// <c>xeno.analytics.query.duration</c> and <c>xeno.analytics.records.scanned</c> meters. Thread safe.
    /// </summary>
    public static class UsageAnalyticsRunner
    {
        /// <summary>
        /// Build analytics from a record stream.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <param name="records">Records.</param>
        /// <param name="query">Query.</param>
        /// <param name="options">Options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics.</returns>
        /// <exception cref="ArgumentNullException">records or query is null.</exception>
        /// <exception cref="ArgumentException">The query is not valid.</exception>
        public static async Task<UsageAnalytics> RunAsync(UsageDatasetEnum dataset, IAsyncEnumerable<UsageRecord> records, UsageAnalyticsQuery query, UsageAnalyticsOptions? options, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentNullException.ThrowIfNull(query);
            Stopwatch clock = Stopwatch.StartNew();
            string name = dataset.ToString();
            using (Activity? activity = XenoTelemetry.ActivitySource.StartActivity("analytics usage"))
            {
                activity?.SetTag("xeno.analytics.dataset", name);
                try
                {
                    UsageAnalytics analytics = await UsageAnalyticsBuilder.BuildAsync(dataset, records, query, options, token).ConfigureAwait(false);
                    activity?.SetTag("xeno.analytics.records", analytics.RecordsScanned);
                    activity?.SetTag("xeno.analytics.truncated", analytics.Truncated);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    XenoTelemetry.RecordAnalyticsQuery(name, analytics.Truncated ? "truncated" : "ok", clock.Elapsed.TotalSeconds, analytics.RecordsScanned);
                    return analytics;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                    XenoTelemetry.RecordAnalyticsQuery(name, "error", clock.Elapsed.TotalSeconds, 0);
                    throw;
                }
            }
        }
    }
}

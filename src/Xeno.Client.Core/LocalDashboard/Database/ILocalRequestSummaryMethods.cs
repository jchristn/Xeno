namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// Local request summary database methods.
    /// </summary>
    public interface ILocalRequestSummaryMethods
    {
        /// <summary>
        /// Summarize request history over time.
        /// </summary>
        /// <param name="fromUtc">Inclusive start timestamp.</param>
        /// <param name="toUtc">Exclusive end timestamp.</param>
        /// <param name="bucketMinutes">Bucket width in minutes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary buckets.</returns>
        Task<IReadOnlyList<LocalRequestHistorySummaryBucket>> SummarizeAsync(DateTime fromUtc, DateTime toUtc, int bucketMinutes, CancellationToken token);

        /// <summary>
        /// Retrieve aggregate statistics.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statistics.</returns>
        Task<LocalDashboardStatisticsResponse> StatisticsAsync(CancellationToken token);
    }
}

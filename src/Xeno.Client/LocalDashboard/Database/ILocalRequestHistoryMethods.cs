namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// Local request history database methods.
    /// </summary>
    public interface ILocalRequestHistoryMethods
    {
        /// <summary>
        /// Initialize local request-history storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// Upsert a local request-history entry.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <param name="settings">Retention settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpsertAsync(LocalRequestHistoryEntry entry, ClientLocalHistorySettings settings, CancellationToken token);

        /// <summary>
        /// Retrieve recent request-history entries.
        /// </summary>
        /// <param name="limit">Maximum number of entries.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries.</returns>
        Task<IReadOnlyList<LocalRequestHistoryEntry>> ListAsync(int limit, CancellationToken token);

        /// <summary>
        /// Retrieve one request-history entry.
        /// </summary>
        /// <param name="id">Request identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry, if found.</returns>
        Task<LocalRequestHistoryEntry?> GetAsync(string id, CancellationToken token);

        /// <summary>
        /// Delete a request-history entry.
        /// </summary>
        /// <param name="id">Request identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token);

        /// <summary>
        /// Delete all request-history entries.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteAllAsync(CancellationToken token);

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

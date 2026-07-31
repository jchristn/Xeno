namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Persisted request history database methods. Request history lives in its own table and is
    /// never cached in the in-memory platform state.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Insert a request history entry.
        /// </summary>
        /// <param name="entry">Entry to persist.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InsertAsync(RequestHistoryEntry entry, CancellationToken token);

        /// <summary>
        /// Read a single request history entry by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">Owning tenant identifier.</param>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The entry, or null when not found.</returns>
        Task<RequestHistoryEntry?> GetAsync(string tenantId, string id, CancellationToken token);

        /// <summary>
        /// Query request history entries with paging and total count.
        /// </summary>
        /// <param name="query">Filter and paging options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching page and total count.</returns>
        Task<RequestHistoryPage> QueryAsync(RequestHistoryQuery query, CancellationToken token);

        /// <summary>
        /// Count request history entries matching a filter.
        /// </summary>
        /// <param name="query">Filter options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching count.</returns>
        Task<int> CountAsync(RequestHistoryQuery query, CancellationToken token);

        /// <summary>
        /// Delete request history entries matching a filter.
        /// </summary>
        /// <param name="query">Filter options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteAsync(RequestHistoryQuery query, CancellationToken token);

        /// <summary>
        /// Anonymize user references on entries that reference the specified user, credentials, or sessions.
        /// </summary>
        /// <param name="tenantId">Owning tenant identifier.</param>
        /// <param name="userId">User identifier being anonymized.</param>
        /// <param name="credentialIds">Credential identifiers owned by the user.</param>
        /// <param name="sessionIds">Session identifiers owned by the user.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated row count.</returns>
        Task<int> AnonymizeUserAsync(string tenantId, string userId, System.Collections.Generic.IReadOnlyCollection<string> credentialIds, System.Collections.Generic.IReadOnlyCollection<string> sessionIds, CancellationToken token);

        /// <summary>
        /// Delete request history entries created strictly before the cutoff.
        /// </summary>
        /// <param name="cutoffUtc">Exclusive upper bound on creation time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> PruneOlderThanAsync(DateTime cutoffUtc, CancellationToken token);
    }
}

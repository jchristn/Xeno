namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Persisted audit record database methods. Audit records live in their own table and are never
    /// cached in the in-memory platform state.
    /// </summary>
    public interface IAuditMethods
    {
        /// <summary>
        /// Insert an audit record.
        /// </summary>
        /// <param name="record">Record to persist.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InsertAsync(AuditRecord record, CancellationToken token);

        /// <summary>
        /// Query audit records with paging and total count.
        /// </summary>
        /// <param name="query">Filter and paging options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching page and total count.</returns>
        Task<AuditRecordPage> QueryAsync(AuditQuery query, CancellationToken token);

        /// <summary>
        /// Count audit records matching a filter.
        /// </summary>
        /// <param name="query">Filter options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching count.</returns>
        Task<int> CountAsync(AuditQuery query, CancellationToken token);

        /// <summary>
        /// Delete audit records matching a filter.
        /// </summary>
        /// <param name="query">Filter options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteAsync(AuditQuery query, CancellationToken token);

        /// <summary>
        /// Delete audit records created strictly before the cutoff.
        /// </summary>
        /// <param name="cutoffUtc">Exclusive upper bound on creation time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> PruneOlderThanAsync(DateTime cutoffUtc, CancellationToken token);
    }
}

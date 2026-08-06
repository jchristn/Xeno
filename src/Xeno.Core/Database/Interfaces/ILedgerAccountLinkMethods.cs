namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Ledger account link data access methods. A <see cref="LedgerAccountLink"/> associates a Xeno
    /// user (identified by <see cref="LedgerAccountLink.XenoTenantId"/> and
    /// <see cref="LedgerAccountLink.XenoUserId"/>) with a NetLedger account and is persisted in the
    /// flat table <c>ledger_account_links</c>. No domain object is stored as a JSON document.
    /// </summary>
    public interface ILedgerAccountLinkMethods
    {
        /// <summary>
        /// Create a ledger account link. The link's <see cref="LedgerAccountLink.Id"/> is backfilled
        /// when the caller leaves it empty.
        /// </summary>
        /// <param name="link">Ledger account link to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created ledger account link.</returns>
        Task<LedgerAccountLink> CreateAsync(LedgerAccountLink link, CancellationToken token = default);

        /// <summary>
        /// Read a ledger account link by identifier.
        /// </summary>
        /// <param name="id">Ledger account link identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ledger account link, or null when not found.</returns>
        Task<LedgerAccountLink?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read the ledger account link owned by a specific Xeno user.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="xenoUserId">Xeno user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ledger account link, or null when not found.</returns>
        Task<LedgerAccountLink?> ReadByUserAsync(string xenoTenantId, string xenoUserId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all ledger account links owned by a specific Xeno tenant.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All ledger account links for the tenant.</returns>
        Task<List<LedgerAccountLink>> EnumerateByTenantAsync(string xenoTenantId, CancellationToken token = default);

        /// <summary>
        /// Update a ledger account link. The row's <c>updated_utc</c> is advanced to the current UTC time.
        /// </summary>
        /// <param name="link">Ledger account link to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated ledger account link.</returns>
        Task<LedgerAccountLink> UpdateAsync(LedgerAccountLink link, CancellationToken token = default);

        /// <summary>
        /// Delete a ledger account link by identifier.
        /// </summary>
        /// <param name="id">Ledger account link identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete all ledger account links owned by a specific Xeno tenant.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string xenoTenantId, CancellationToken token = default);

        /// <summary>
        /// Delete the ledger account link owned by a specific Xeno user.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="xenoUserId">Xeno user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByUserAsync(string xenoTenantId, string xenoUserId, CancellationToken token = default);
    }
}

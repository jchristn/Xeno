namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Ledger tenant link data access methods. A <see cref="LedgerTenantLink"/> is owned by a Xeno
    /// tenant and maps that tenant to a NetLedger tenant. Each Xeno tenant has at most one link, so
    /// the <c>xeno_tenant_id</c> column carries a unique index.
    /// </summary>
    public interface ILedgerTenantLinkMethods
    {
        /// <summary>
        /// Create a ledger tenant link. The link's <see cref="LedgerTenantLink.Id"/> is backfilled
        /// when the caller leaves it empty.
        /// </summary>
        /// <param name="link">Ledger tenant link to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created ledger tenant link.</returns>
        Task<LedgerTenantLink> CreateAsync(LedgerTenantLink link, CancellationToken token = default);

        /// <summary>
        /// Read a ledger tenant link by identifier.
        /// </summary>
        /// <param name="id">Ledger tenant link identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ledger tenant link, or null when not found.</returns>
        Task<LedgerTenantLink?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read the ledger tenant link that belongs to a Xeno tenant.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ledger tenant link, or null when not found.</returns>
        Task<LedgerTenantLink?> ReadByTenantAsync(string xenoTenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all ledger tenant links.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All ledger tenant links.</returns>
        Task<List<LedgerTenantLink>> EnumerateAsync(CancellationToken token = default);

        /// <summary>
        /// Update a ledger tenant link. The row's <c>updated_utc</c> is advanced to the current UTC time.
        /// </summary>
        /// <param name="link">Ledger tenant link to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated ledger tenant link.</returns>
        Task<LedgerTenantLink> UpdateAsync(LedgerTenantLink link, CancellationToken token = default);

        /// <summary>
        /// Delete a ledger tenant link by identifier.
        /// </summary>
        /// <param name="id">Ledger tenant link identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete the ledger tenant link that belongs to a Xeno tenant.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string xenoTenantId, CancellationToken token = default);
    }
}

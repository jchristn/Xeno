namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Tenant data access methods. Tenants are the root of the multi-tenancy tree and are therefore
    /// a global (non-tenant-scoped) table. The tenant's composite <see cref="Tenant.SubordinateServices"/>
    /// collection is persisted in the dedicated child table <c>tenant_subordinate_services</c>.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>
        /// Create a tenant and its subordinate-service child rows. The tenant's <see cref="Tenant.Id"/>
        /// is backfilled when the caller leaves it empty.
        /// </summary>
        /// <param name="tenant">Tenant to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created tenant.</returns>
        Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Read a tenant by identifier, including its subordinate-service child rows.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant, or null when not found.</returns>
        Task<Tenant?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate all tenants, including their subordinate-service child rows.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All tenants.</returns>
        Task<List<Tenant>> EnumerateAsync(CancellationToken token = default);

        /// <summary>
        /// Update a tenant and replace its subordinate-service child rows. The row's
        /// <c>last_update_utc</c> is advanced to the current UTC time.
        /// </summary>
        /// <param name="tenant">Tenant to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated tenant.</returns>
        Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Delete a tenant and its subordinate-service child rows. Deleting the tenant row does not
        /// cascade to other tenant-owned tables; callers that need a full purge use the tenant-nuke path.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a tenant exists.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the tenant exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);
    }
}

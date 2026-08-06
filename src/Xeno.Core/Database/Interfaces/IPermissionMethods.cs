namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Permission data access methods. Permissions are tenant-owned and are therefore a tenant-scoped
    /// table; every query filters by <see cref="Permission.TenantId"/>. The composite of
    /// (tenant_id, resource_type, operation_type) is unique.
    /// </summary>
    public interface IPermissionMethods
    {
        /// <summary>
        /// Create a permission. The permission's <see cref="Permission.Id"/> is backfilled when the
        /// caller leaves it empty.
        /// </summary>
        /// <param name="permission">Permission to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created permission.</returns>
        Task<Permission> CreateAsync(Permission permission, CancellationToken token = default);

        /// <summary>
        /// Read a permission by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Permission identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Permission, or null when not found.</returns>
        Task<Permission?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate all permissions belonging to a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All permissions owned by the tenant.</returns>
        Task<List<Permission>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a permission. The row's <c>last_update_utc</c> is advanced to the current UTC time.
        /// </summary>
        /// <param name="permission">Permission to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated permission.</returns>
        Task<Permission> UpdateAsync(Permission permission, CancellationToken token = default);

        /// <summary>
        /// Delete a permission by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Permission identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a permission exists within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Permission identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the permission exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}

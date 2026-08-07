namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Role data access methods. Roles are tenant-owned and are therefore a tenant-scoped table; every
    /// query filters by tenant identifier. The role's <see cref="Role.PermissionIds"/> collection is
    /// persisted in the dedicated child table <c>role_permissions</c>.
    /// </summary>
    public interface IRoleMethods
    {
        /// <summary>
        /// Create a role and its permission child rows. The role's <see cref="Role.Id"/> is backfilled
        /// when the caller leaves it empty.
        /// </summary>
        /// <param name="role">Role to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created role.</returns>
        Task<Role> CreateAsync(Role role, CancellationToken token = default);

        /// <summary>
        /// Read a role by identifier, including its permission child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role, or null when not found.</returns>
        Task<Role?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a role by name within a tenant, including its permission child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="name">Role name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role, or null when not found.</returns>
        Task<Role?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Enumerate all roles for a tenant, including their permission child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All roles for the tenant.</returns>
        Task<List<Role>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a role and replace its permission child rows. The row's <c>last_update_utc</c> is
        /// advanced to the current UTC time.
        /// </summary>
        /// <param name="role">Role to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated role.</returns>
        Task<Role> UpdateAsync(Role role, CancellationToken token = default);

        /// <summary>
        /// Delete a role and its permission child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete every role owned by a tenant (and its role-permission rows) in a single set-based operation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of role rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Determine whether a role exists.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the role exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}

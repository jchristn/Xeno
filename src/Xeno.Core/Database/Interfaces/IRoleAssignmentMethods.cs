namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Role assignment data access methods. Role assignments are tenant-owned rows on
    /// <c>role_assignments</c> that bind a role to a principal within a tenant. Every query is
    /// tenant-scoped and filters by <c>tenant_id</c>.
    /// </summary>
    public interface IRoleAssignmentMethods
    {
        /// <summary>
        /// Create a role assignment. The assignment's <see cref="RoleAssignment.Id"/> is backfilled
        /// when the caller leaves it empty.
        /// </summary>
        /// <param name="assignment">Role assignment to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created role assignment.</returns>
        Task<RoleAssignment> CreateAsync(RoleAssignment assignment, CancellationToken token = default);

        /// <summary>
        /// Read a role assignment by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Role assignment identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role assignment, or null when not found.</returns>
        Task<RoleAssignment?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate all role assignments for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role assignments for the tenant.</returns>
        Task<List<RoleAssignment>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all role assignments for a tenant that bind the given role.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="roleId">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role assignments for the tenant and role.</returns>
        Task<List<RoleAssignment>> EnumerateByRoleAsync(string tenantId, string roleId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all role assignments for a tenant that bind the given principal.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="principalId">Principal identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Role assignments for the tenant and principal.</returns>
        Task<List<RoleAssignment>> EnumerateByPrincipalAsync(string tenantId, string principalId, CancellationToken token = default);

        /// <summary>
        /// Update a role assignment. The row is fully upserted and its <c>last_update_utc</c> is
        /// advanced to the current UTC time.
        /// </summary>
        /// <param name="assignment">Role assignment to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated role assignment.</returns>
        Task<RoleAssignment> UpdateAsync(RoleAssignment assignment, CancellationToken token = default);

        /// <summary>
        /// Delete a role assignment by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Role assignment identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete all role assignments for a tenant that bind the given role.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="roleId">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByRoleAsync(string tenantId, string roleId, CancellationToken token = default);
    }
}

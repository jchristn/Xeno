namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// User data access methods. Users are tenant-owned; every read, update, and delete is scoped by
    /// <see cref="User.TenantId"/> to prevent cross-tenant leakage. User scalar fields map to columns
    /// on <c>users</c>; no domain object is stored as a JSON document.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>
        /// Create a user. The user's <see cref="User.Id"/> is backfilled when the caller leaves it empty.
        /// </summary>
        /// <param name="user">User to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created user.</returns>
        Task<User> CreateAsync(User user, CancellationToken token = default);

        /// <summary>
        /// Read a user by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User, or null when not found.</returns>
        Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a user by username within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="username">Username.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User, or null when not found.</returns>
        Task<User?> ReadByUsernameAsync(string tenantId, string username, CancellationToken token = default);

        /// <summary>
        /// Read a user by email address within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User, or null when not found.</returns>
        Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Enumerate all users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All users within the tenant.</returns>
        Task<List<User>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a user. Performs a full upsert of the user row.
        /// </summary>
        /// <param name="user">User to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated user.</returns>
        Task<User> UpdateAsync(User user, CancellationToken token = default);

        /// <summary>
        /// Delete a user by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a user exists within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the user exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}

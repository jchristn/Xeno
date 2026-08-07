namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Credential data access methods. Credentials are tenant-owned and user-owned; every tenant-scoped
    /// query is filtered by <see cref="Credential.TenantId"/>. Credentials are stored as scalar rows on
    /// the <c>credentials</c> table with no child tables and no JSON document.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>
        /// Create a credential. The credential's <see cref="Credential.Id"/> is backfilled when the
        /// caller leaves it empty.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created credential.</returns>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Read a credential by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential, or null when not found.</returns>
        Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a credential by access key within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential, or null when not found.</returns>
        Task<Credential?> ReadByAccessKeyAsync(string tenantId, string accessKey, CancellationToken token = default);

        /// <summary>
        /// Enumerate all credentials that share an access key across every tenant. Access keys are not
        /// globally unique, so this lookup is intentionally not tenant-scoped and returns all matches.
        /// </summary>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All credentials with the given access key.</returns>
        Task<List<Credential>> EnumerateByAccessKeyAsync(string accessKey, CancellationToken token = default);

        /// <summary>
        /// Enumerate all credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All credentials for the tenant.</returns>
        Task<List<Credential>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all credentials owned by a user within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">Owning user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All credentials for the user within the tenant.</returns>
        Task<List<Credential>> EnumerateByUserAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Update a credential. Performs a full upsert of the credential row.
        /// </summary>
        /// <param name="credential">Credential to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated credential.</returns>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Delete a credential by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete every credential owned by a tenant in a single set-based statement.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Delete every credential owned by a specific user in a single set-based statement.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">Owning user identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByUserAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Determine whether a credential exists within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the credential exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}

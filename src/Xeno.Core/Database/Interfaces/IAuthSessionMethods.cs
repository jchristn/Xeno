namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Authenticated-session data access methods. Sessions are tenant-owned and map to the
    /// <c>auth_sessions</c> table. The <c>token_hash</c> column is globally unique, so lookups by
    /// token hash and expiry-based pruning are intentionally not tenant-scoped.
    /// </summary>
    public interface IAuthSessionMethods
    {
        /// <summary>
        /// Create an authenticated session. The session's <see cref="AuthSession.Id"/> is backfilled
        /// when the caller leaves it empty.
        /// </summary>
        /// <param name="session">Session to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created session.</returns>
        Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default);

        /// <summary>
        /// Read a session by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Session, or null when not found.</returns>
        Task<AuthSession?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a session by its globally unique token hash. This lookup is intentionally not
        /// tenant-scoped.
        /// </summary>
        /// <param name="tokenHash">Token hash.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Session, or null when not found.</returns>
        Task<AuthSession?> ReadByTokenHashAsync(string tokenHash, CancellationToken token = default);

        /// <summary>
        /// Read a session by its identifier. This lookup is intentionally not tenant-scoped.
        /// </summary>
        /// <param name="id">Session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Session, or null when not found.</returns>
        Task<AuthSession?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate all sessions owned by a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All sessions for the tenant.</returns>
        Task<List<AuthSession>> EnumerateByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a session. The row's <c>last_update_utc</c> is advanced to the current UTC time.
        /// </summary>
        /// <param name="session">Session to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated session.</returns>
        Task<AuthSession> UpdateAsync(AuthSession session, CancellationToken token = default);

        /// <summary>
        /// Delete a session by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete every auth session owned by a tenant in a single set-based statement.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Delete all sessions owned by a tenant for a given user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByUserAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Delete all sessions owned by a tenant for a given credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> DeleteByCredentialAsync(string tenantId, string credentialId, CancellationToken token = default);

        /// <summary>
        /// Delete all sessions whose expiry is earlier than the supplied cutoff. This operation is
        /// intentionally not tenant-scoped.
        /// </summary>
        /// <param name="cutoffUtc">Cutoff timestamp; sessions expiring before this are deleted.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        Task<int> PruneExpiredAsync(DateTime cutoffUtc, CancellationToken token = default);
    }
}

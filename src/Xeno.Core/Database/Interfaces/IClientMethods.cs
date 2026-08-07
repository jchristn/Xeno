namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Client data access methods. Clients are tenant-owned. A client's scalar fields map to columns on
    /// <c>clients</c>, and its two ordered composite collections
    /// (<see cref="XenoClientRecord.Providers"/> and <see cref="XenoClientRecord.Models"/>) map to the
    /// ordered child tables <c>client_providers</c> and <c>client_models</c>. No domain object is stored
    /// as a JSON document.
    /// </summary>
    public interface IClientMethods
    {
        /// <summary>
        /// Create a client and its provider and model child rows. The client's <see cref="XenoClientRecord.Id"/>
        /// is backfilled when the caller leaves it empty.
        /// </summary>
        /// <param name="client">Client to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created client.</returns>
        Task<XenoClientRecord> CreateAsync(XenoClientRecord client, CancellationToken token = default);

        /// <summary>
        /// Read a client by identifier within a tenant, including its provider and model child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Client identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Client, or null when not found.</returns>
        Task<XenoClientRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a client by name within a tenant, including its provider and model child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="name">Client name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Client, or null when not found.</returns>
        Task<XenoClientRecord?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Enumerate all clients owned by a tenant, including their provider and model child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All clients owned by the tenant.</returns>
        Task<List<XenoClientRecord>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all shared clients across every tenant, including their provider and model child rows.
        /// This query is intentionally global (not tenant-scoped) to support cross-tenant shared-client
        /// consumption.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All shared clients across all tenants.</returns>
        Task<List<XenoClientRecord>> EnumerateSharedAsync(CancellationToken token = default);

        /// <summary>
        /// Update a client and replace its provider and model child rows.
        /// </summary>
        /// <param name="client">Client to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated client.</returns>
        Task<XenoClientRecord> UpdateAsync(XenoClientRecord client, CancellationToken token = default);

        /// <summary>
        /// Update only a client's connection state (connected flag and last-heartbeat timestamp) without
        /// touching its provider or model child rows. This is the hot-path heartbeat update; using the full
        /// <see cref="UpdateAsync"/> here would replace the child tables and could clobber a concurrent
        /// inventory update.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Client identifier.</param>
        /// <param name="connected">Whether the client is currently connected.</param>
        /// <param name="lastHeartbeatUtc">Last heartbeat timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was updated.</returns>
        Task<bool> UpdateHeartbeatAsync(string tenantId, string id, bool connected, System.DateTime lastHeartbeatUtc, CancellationToken token = default);

        /// <summary>
        /// Atomically adjust a client's in-flight request counter by a signed delta, clamped at zero,
        /// without touching its child rows. Uses a single UPDATE so concurrent relays on multiple nodes
        /// do not lose increments or decrements (a read-modify-write here would race and could clobber the
        /// child tables).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Client identifier.</param>
        /// <param name="delta">Signed amount to add to the in-flight counter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AdjustInFlightAsync(string tenantId, string id, int delta, CancellationToken token = default);

        /// <summary>
        /// Delete a client and its provider and model child rows within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Client identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete every client owned by a tenant (and their advertised provider/model child rows) in a single
        /// set-based operation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of client rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Determine whether a client exists within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Client identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the client exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}

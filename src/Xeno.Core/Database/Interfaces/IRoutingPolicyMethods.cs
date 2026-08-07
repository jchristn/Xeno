namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Routing policy data access methods. Routing policies are tenant-scoped and every query filters by
    /// tenant. The policy's owned <see cref="RoutingPolicy.Adaptive"/> composite is persisted as columns on
    /// the parent <c>routing_policies</c> row, while the ordered <see cref="RoutingPolicy.TelemetryFilters"/>
    /// and <see cref="RoutingPolicy.TelemetryRanking"/> collections are persisted in the dedicated child
    /// tables <c>routing_policy_filters</c> and <c>routing_policy_ranking_rules</c>.
    /// </summary>
    public interface IRoutingPolicyMethods
    {
        /// <summary>
        /// Create a routing policy along with its filter and ranking child rows. The policy's
        /// <see cref="RoutingPolicy.Id"/> is backfilled when the caller leaves it empty.
        /// </summary>
        /// <param name="policy">Routing policy to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created routing policy.</returns>
        Task<RoutingPolicy> CreateAsync(RoutingPolicy policy, CancellationToken token = default);

        /// <summary>
        /// Read a routing policy by tenant and identifier, including its filter and ranking child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Routing policy identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Routing policy, or null when not found.</returns>
        Task<RoutingPolicy?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read the tenant's default routing policy, including its filter and ranking child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Default routing policy, or null when none is flagged as default.</returns>
        Task<RoutingPolicy?> ReadDefaultAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate all routing policies for a tenant, including their filter and ranking child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All routing policies for the tenant.</returns>
        Task<List<RoutingPolicy>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a routing policy and replace its filter and ranking child rows. The row's
        /// <c>last_update_utc</c> is advanced to the current UTC time.
        /// </summary>
        /// <param name="policy">Routing policy to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated routing policy.</returns>
        Task<RoutingPolicy> UpdateAsync(RoutingPolicy policy, CancellationToken token = default);

        /// <summary>
        /// Delete a routing policy and its filter and ranking child rows.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Routing policy identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete every routing policy owned by a tenant (and its filter/ranking child rows) in a single
        /// set-based operation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of routing policy rows deleted.</returns>
        Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Determine whether a routing policy exists for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Routing policy identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the routing policy exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
    }
}

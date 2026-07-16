namespace Xeno.Core.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Tenant lifecycle hooks implemented by subordinate services.
    /// </summary>
    public interface ITenantSubordinateService
    {
        /// <summary>
        /// Service name.
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Create subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="seedUsers">Users to provision with the tenant.</param>
        /// <param name="allowDeferredProvisioning">Whether provisioning failures can be reported instead of thrown.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<TenantSubordinateServiceStatus> CreateAsync(Tenant tenant, IReadOnlyList<User> seedUsers, bool allowDeferredProvisioning, CancellationToken token = default);

        /// <summary>
        /// Update subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<TenantSubordinateServiceStatus> UpdateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Suspend subordinate tenant writes.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<TenantSubordinateServiceStatus> SuspendAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Reactivate subordinate tenant writes.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<TenantSubordinateServiceStatus> ReactivateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Delete subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="hardDelete">Whether data must be permanently removed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<TenantSubordinateServiceStatus> DeleteAsync(Tenant tenant, bool hardDelete, CancellationToken token = default);

        /// <summary>
        /// Reconcile subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<TenantSubordinateServiceStatus> ReconcileAsync(Tenant tenant, CancellationToken token = default);
    }
}

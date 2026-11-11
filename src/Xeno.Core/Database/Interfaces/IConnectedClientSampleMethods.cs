namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Analytics;

    /// <summary>
    /// Stored counts of each tenant's connected clients.
    /// </summary>
    public interface IConnectedClientSampleMethods
    {
        /// <summary>
        /// Store a sample.
        /// </summary>
        /// <param name="sample">Sample.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InsertAsync(ConnectedClientSample sample, CancellationToken token);

        /// <summary>
        /// A tenant's samples in a window, oldest first.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="fromUtc">Inclusive start.</param>
        /// <param name="toUtc">Exclusive end.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Samples.</returns>
        Task<List<ConnectedClientSample>> ListAsync(string tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken token);

        /// <summary>
        /// Delete samples older than a time.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows deleted.</returns>
        Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token);

        /// <summary>
        /// Delete every sample in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByTenantAsync(string tenantId, CancellationToken token);
    }
}

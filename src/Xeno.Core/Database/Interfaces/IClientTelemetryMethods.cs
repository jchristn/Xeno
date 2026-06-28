namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models.Telemetry;

    /// <summary>
    /// Server-collected host telemetry database methods.
    /// </summary>
    public interface IClientTelemetryMethods
    {
        /// <summary>
        /// Persist a host telemetry sample.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Client identifier.</param>
        /// <param name="clientName">Client display name.</param>
        /// <param name="collectionRequestId">Collection request identifier.</param>
        /// <param name="snapshot">Telemetry snapshot.</param>
        /// <param name="snapshotJson">Optional bounded raw JSON.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Persisted host telemetry sample.</returns>
        Task<ServerClientTelemetrySampleRecord> CreateHostSampleAsync(string tenantId, string clientId, string? clientName, string? collectionRequestId, ClientTelemetrySnapshot snapshot, string? snapshotJson, CancellationToken token);

        /// <summary>
        /// Read a host telemetry detail by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Telemetry detail, if found.</returns>
        Task<ServerClientTelemetrySampleDetail?> ReadHostDetailAsync(string tenantId, string id, CancellationToken token);

        /// <summary>
        /// Search host telemetry samples.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result.</returns>
        Task<ServerClientTelemetrySearchResult> SearchHostAsync(ServerClientTelemetrySearchRequest request, CancellationToken token);

        /// <summary>
        /// Roll up host telemetry samples.
        /// </summary>
        /// <param name="request">Rollup request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rollup buckets.</returns>
        Task<IReadOnlyList<ServerClientTelemetryRollupBucket>> RollupHostAsync(ServerClientTelemetryRollupRequest request, CancellationToken token);

        /// <summary>
        /// Read latest host telemetry by tenant and optional client.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Optional client identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest samples.</returns>
        Task<IReadOnlyList<ServerClientTelemetrySampleRecord>> LatestHostByClientAsync(string tenantId, string? clientId, CancellationToken token);

        /// <summary>
        /// Delete one host telemetry sample.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteHostAsync(string tenantId, string id, CancellationToken token);

        /// <summary>
        /// Bulk delete host telemetry.
        /// </summary>
        /// <param name="request">Delete request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteHostBulkAsync(ServerClientTelemetryDeleteRequest request, CancellationToken token);

        /// <summary>
        /// Delete host telemetry older than a timestamp.
        /// </summary>
        /// <param name="olderThanUtc">Oldest retained timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token);
    }
}

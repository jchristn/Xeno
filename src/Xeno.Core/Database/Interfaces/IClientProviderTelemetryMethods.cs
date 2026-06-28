namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models.Telemetry;

    /// <summary>
    /// Server-collected provider telemetry database methods.
    /// </summary>
    public interface IClientProviderTelemetryMethods
    {
        /// <summary>
        /// Persist a provider telemetry snapshot.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Client identifier.</param>
        /// <param name="clientName">Client display name.</param>
        /// <param name="collectionRequestId">Collection request identifier.</param>
        /// <param name="snapshot">Provider telemetry snapshot.</param>
        /// <param name="persistRawSnapshots">Whether raw snapshots should be persisted.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Persisted provider telemetry samples.</returns>
        Task<IReadOnlyList<ServerProviderTelemetrySampleRecord>> CreateProviderSnapshotAsync(string tenantId, string clientId, string? clientName, string? collectionRequestId, ClientProviderTelemetrySnapshot snapshot, bool persistRawSnapshots, CancellationToken token);

        /// <summary>
        /// Read provider telemetry detail by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider telemetry detail, if found.</returns>
        Task<ServerProviderTelemetrySampleDetail?> ReadProviderDetailAsync(string tenantId, string id, CancellationToken token);

        /// <summary>
        /// Search provider telemetry samples.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result.</returns>
        Task<ServerProviderTelemetrySearchResult> SearchProviderAsync(ServerProviderTelemetrySearchRequest request, CancellationToken token);

        /// <summary>
        /// Roll up provider telemetry samples.
        /// </summary>
        /// <param name="request">Rollup request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rollup buckets.</returns>
        Task<IReadOnlyList<ServerProviderTelemetryRollupBucket>> RollupProviderAsync(ServerProviderTelemetryRollupRequest request, CancellationToken token);

        /// <summary>
        /// Read latest provider telemetry by tenant and optional client.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Optional client identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest provider telemetry samples.</returns>
        Task<IReadOnlyList<ServerProviderTelemetrySampleRecord>> LatestProviderByClientAsync(string tenantId, string? clientId, CancellationToken token);

        /// <summary>
        /// Delete one provider telemetry sample.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Sample identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteProviderAsync(string tenantId, string id, CancellationToken token);

        /// <summary>
        /// Bulk delete provider telemetry.
        /// </summary>
        /// <param name="request">Delete request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteProviderBulkAsync(ServerClientTelemetryDeleteRequest request, CancellationToken token);

        /// <summary>
        /// Delete provider telemetry older than a timestamp.
        /// </summary>
        /// <param name="olderThanUtc">Oldest retained timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token);
    }
}

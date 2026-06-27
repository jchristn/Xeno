namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Client telemetry history database methods.
    /// </summary>
    public interface IClientTelemetryHistoryMethods
    {
        /// <summary>
        /// Initialize telemetry storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// Persist a telemetry snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Persisted sample record.</returns>
        Task<ClientTelemetrySampleRecord> CreateAsync(ClientTelemetrySnapshot snapshot, ClientTelemetrySettings settings, CancellationToken token);

        /// <summary>
        /// Read latest telemetry detail.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest detail.</returns>
        Task<ClientTelemetrySampleDetail?> ReadLatestAsync(CancellationToken token);

        /// <summary>
        /// Read telemetry detail by ID.
        /// </summary>
        /// <param name="id">Telemetry sample ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detail.</returns>
        Task<ClientTelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token);

        /// <summary>
        /// Search telemetry samples.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result.</returns>
        Task<ClientTelemetrySearchResult> SearchAsync(ClientTelemetrySearchRequest request, CancellationToken token);

        /// <summary>
        /// Roll up telemetry samples.
        /// </summary>
        /// <param name="request">Rollup request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Buckets.</returns>
        Task<IReadOnlyList<ClientTelemetryRollupBucket>> RollupAsync(ClientTelemetryRollupRequest request, CancellationToken token);

        /// <summary>
        /// Delete a telemetry sample.
        /// </summary>
        /// <param name="id">Sample ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token);

        /// <summary>
        /// Delete all telemetry samples.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted rows.</returns>
        Task<int> DeleteAllAsync(CancellationToken token);

        /// <summary>
        /// Delete samples older than a timestamp.
        /// </summary>
        /// <param name="olderThanUtc">Oldest retained timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted rows.</returns>
        Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token);

        /// <summary>
        /// Get persistence status.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<ClientTelemetryPersistenceStatus> GetStatusAsync(ClientTelemetrySettings settings, CancellationToken token);
    }
}

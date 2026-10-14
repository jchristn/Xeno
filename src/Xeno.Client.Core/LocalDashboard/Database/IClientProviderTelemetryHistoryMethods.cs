namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Client provider telemetry history database methods.
    /// </summary>
    public interface IClientProviderTelemetryHistoryMethods
    {
        /// <summary>
        /// Initialize provider telemetry storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// Persist provider telemetry.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Persisted records.</returns>
        Task<IReadOnlyList<ClientProviderTelemetrySampleRecord>> CreateAsync(ClientProviderTelemetrySnapshot snapshot, ClientTelemetrySettings settings, CancellationToken token);

        /// <summary>
        /// Read latest provider telemetry detail.
        /// </summary>
        /// <param name="providerName">Optional provider name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest detail.</returns>
        Task<ClientProviderTelemetrySampleDetail?> ReadLatestAsync(string? providerName, CancellationToken token);

        /// <summary>
        /// Read provider telemetry detail.
        /// </summary>
        /// <param name="id">Sample ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detail.</returns>
        Task<ClientProviderTelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token);

        /// <summary>
        /// Search provider telemetry.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result.</returns>
        Task<ClientProviderTelemetrySearchResult> SearchAsync(ClientProviderTelemetrySearchRequest request, CancellationToken token);

        /// <summary>
        /// Roll up provider telemetry.
        /// </summary>
        /// <param name="request">Rollup request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Buckets.</returns>
        Task<IReadOnlyList<ClientProviderTelemetryRollupBucket>> RollupAsync(ClientProviderTelemetryRollupRequest request, CancellationToken token);

        /// <summary>
        /// Delete a provider telemetry sample.
        /// </summary>
        /// <param name="id">Sample ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token);

        /// <summary>
        /// Delete all provider telemetry samples.
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
        /// Get status.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        Task<ClientTelemetryPersistenceStatus> GetStatusAsync(ClientTelemetrySettings settings, CancellationToken token);
    }
}

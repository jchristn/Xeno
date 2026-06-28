namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models.Telemetry;

    /// <summary>
    /// Server telemetry collection attempt database methods.
    /// </summary>
    public interface IClientTelemetryCollectionAttemptMethods
    {
        /// <summary>
        /// Create a telemetry collection attempt.
        /// </summary>
        /// <param name="attempt">Attempt record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created attempt.</returns>
        Task<ServerClientTelemetryCollectionAttemptRecord> CreateAttemptAsync(ServerClientTelemetryCollectionAttemptRecord attempt, CancellationToken token);

        /// <summary>
        /// Complete a telemetry collection attempt.
        /// </summary>
        /// <param name="attempt">Attempt record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CompleteAttemptAsync(ServerClientTelemetryCollectionAttemptRecord attempt, CancellationToken token);

        /// <summary>
        /// Search recent attempts.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Optional client identifier.</param>
        /// <param name="limit">Maximum rows.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Attempt records.</returns>
        Task<IReadOnlyList<ServerClientTelemetryCollectionAttemptRecord>> SearchAttemptsAsync(string tenantId, string? clientId, int limit, CancellationToken token);
    }
}

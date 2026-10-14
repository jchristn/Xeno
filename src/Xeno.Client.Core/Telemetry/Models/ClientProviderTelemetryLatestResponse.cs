namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Latest provider telemetry response.
    /// </summary>
    public class ClientProviderTelemetryLatestResponse
    {
        /// <summary>
        /// Latest in-memory snapshot.
        /// </summary>
        public ClientProviderTelemetrySnapshot? Snapshot { get; set; } = null;

        /// <summary>
        /// Latest persisted detail.
        /// </summary>
        public ClientProviderTelemetrySampleDetail? Detail { get; set; } = null;

        /// <summary>
        /// Runtime status.
        /// </summary>
        public ClientTelemetryPersistenceStatus Status { get; set; } = new ClientTelemetryPersistenceStatus();
    }
}

namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Latest telemetry response.
    /// </summary>
    public class ClientTelemetryLatestResponse
    {
        /// <summary>
        /// Latest telemetry snapshot.
        /// </summary>
        public ClientTelemetrySnapshot? Snapshot { get; set; } = null;

        /// <summary>
        /// Latest persisted detail.
        /// </summary>
        public ClientTelemetrySampleDetail? Detail { get; set; } = null;

        /// <summary>
        /// Persistence status.
        /// </summary>
        public ClientTelemetryPersistenceStatus Status { get; set; } = new ClientTelemetryPersistenceStatus();
    }
}

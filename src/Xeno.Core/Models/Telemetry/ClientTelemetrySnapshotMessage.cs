namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Client-to-server host telemetry snapshot frame.
    /// </summary>
    public class ClientTelemetrySnapshotMessage
    {
        /// <summary>
        /// WebSocket frame type.
        /// </summary>
        public string Type { get; set; } = "client.telemetry.snapshot";

        /// <summary>
        /// Telemetry request identifier.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Host telemetry snapshot.
        /// </summary>
        public ClientTelemetrySnapshot Snapshot { get; set; } = new ClientTelemetrySnapshot();
    }
}

namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Client-to-server provider telemetry snapshot frame.
    /// </summary>
    public class ClientProviderTelemetrySnapshotMessage
    {
        /// <summary>
        /// WebSocket frame type.
        /// </summary>
        public string Type { get; set; } = "client.providerTelemetry.snapshot";

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
        /// Provider telemetry snapshot.
        /// </summary>
        public ClientProviderTelemetrySnapshot Snapshot { get; set; } = new ClientProviderTelemetrySnapshot();
    }
}

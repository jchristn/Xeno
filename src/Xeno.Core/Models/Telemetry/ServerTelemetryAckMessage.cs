namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server-to-client telemetry acknowledgement frame.
    /// </summary>
    public class ServerTelemetryAckMessage
    {
        /// <summary>
        /// WebSocket frame type.
        /// </summary>
        public string Type { get; set; } = "server.telemetry.ack";

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
        /// Whether the telemetry frame was accepted.
        /// </summary>
        public bool Accepted { get; set; } = true;

        /// <summary>
        /// Optional safe acknowledgement message.
        /// </summary>
        public string? Message { get; set; } = null;
    }
}

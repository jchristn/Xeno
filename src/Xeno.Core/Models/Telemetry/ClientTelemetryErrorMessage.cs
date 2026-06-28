namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Client-to-server telemetry error frame.
    /// </summary>
    public class ClientTelemetryErrorMessage
    {
        /// <summary>
        /// WebSocket frame type.
        /// </summary>
        public string Type { get; set; } = "client.telemetry.error";

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
        /// Machine-readable error code.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Safe error message.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

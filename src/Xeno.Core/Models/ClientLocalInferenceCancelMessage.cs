namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Client-to-server local inference proxy cancellation sent when the localhost caller disconnects.
    /// </summary>
    public class ClientLocalInferenceCancelMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Request ID to cancel.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID of the local client identity.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID sending the cancellation.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Cancellation reason.
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = "CallerDisconnected";
    }
}

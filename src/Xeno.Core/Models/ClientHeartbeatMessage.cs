namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Client heartbeat message sent over the WebSocket protocol.
    /// </summary>
    public class ClientHeartbeatMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Registered client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant ID for the client session.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Client send timestamp.
        /// </summary>
        [JsonPropertyName("sentUtc")]
        public DateTime? SentUtc { get; set; } = null;
    }
}

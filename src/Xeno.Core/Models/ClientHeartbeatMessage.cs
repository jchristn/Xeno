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

        /// <summary>
        /// Concurrent network requests this client can take right now: the limits of its shared providers minus the slots
        /// its own local requests hold. Null from clients before 0.2.0, which leaves the server's stored capacity unchanged.
        /// </summary>
        public int? NetworkCapacity { get; set; } = null;
    }
}

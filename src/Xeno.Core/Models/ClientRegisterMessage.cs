namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Client registration message sent over the WebSocket protocol.
    /// </summary>
    public class ClientRegisterMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Client ID. The server can assign one when omitted.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant ID for the client.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Human-readable client name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Client hardware, provider, and model inventory.
        /// </summary>
        [JsonPropertyName("inventory")]
        public ClientInventoryMessage? Inventory { get; set; } = null;
    }
}

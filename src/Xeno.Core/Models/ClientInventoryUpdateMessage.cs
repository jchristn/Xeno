namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Client inventory update message sent after local inventory changes.
    /// </summary>
    public class ClientInventoryUpdateMessage : WebSocketMessageEnvelope
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
        /// Updated client inventory.
        /// </summary>
        [JsonPropertyName("inventory")]
        public ClientInventoryMessage? Inventory { get; set; } = null;
    }
}

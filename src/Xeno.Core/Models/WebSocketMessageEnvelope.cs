namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Minimal typed envelope for a WebSocket protocol message.
    /// </summary>
    public class WebSocketMessageEnvelope
    {
        /// <summary>
        /// Stable message type discriminator.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = String.Empty;
    }
}

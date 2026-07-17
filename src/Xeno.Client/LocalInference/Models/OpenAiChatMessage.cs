namespace Xeno.Client.LocalInference.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible chat message.
    /// </summary>
    public class OpenAiChatMessage
    {
        /// <summary>
        /// Message role.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = String.Empty;

        /// <summary>
        /// Message content.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;
    }
}

namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Model advertised by a Xeno client inventory message.
    /// </summary>
    public class ClientInventoryModelMessage
    {
        /// <summary>
        /// Model name exposed to OpenAI-compatible callers.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Whether the model supports completion requests.
        /// </summary>
        [JsonPropertyName("supportsCompletions")]
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Whether the model supports embedding requests.
        /// </summary>
        [JsonPropertyName("supportsEmbeddings")]
        public bool SupportsEmbeddings { get; set; } = false;
    }
}

namespace Xeno.Client.LocalInference.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama-compatible chat request.
    /// </summary>
    public class OllamaChatRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Messages.
        /// </summary>
        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = new List<OllamaChatMessage>();

        /// <summary>
        /// Whether streaming was requested.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool? Stream { get; set; } = null;
    }
}

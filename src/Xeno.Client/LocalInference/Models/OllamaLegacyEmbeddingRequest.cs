namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Legacy Ollama-compatible embeddings request.
    /// </summary>
    public class OllamaLegacyEmbeddingRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Prompt text.
        /// </summary>
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; } = null;
    }
}

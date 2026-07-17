namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Current Ollama-compatible embed request.
    /// </summary>
    public class OllamaEmbedRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Embedding input.
        /// </summary>
        [JsonPropertyName("input")]
        public StringOrStringArray? Input { get; set; } = null;
    }
}

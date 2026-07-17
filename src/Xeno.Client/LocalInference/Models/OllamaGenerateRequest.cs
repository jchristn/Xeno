namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama-compatible generation request.
    /// </summary>
    public class OllamaGenerateRequest
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

        /// <summary>
        /// Whether streaming was requested.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool? Stream { get; set; } = null;
    }
}

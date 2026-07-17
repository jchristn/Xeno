namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama-compatible model show request.
    /// </summary>
    public class OllamaShowRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Model name alias accepted by some clients.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;
    }
}

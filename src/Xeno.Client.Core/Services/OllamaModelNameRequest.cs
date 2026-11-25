namespace Xeno.Client.Services
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Body for Ollama's /api/pull and /api/delete.
    /// </summary>
    public sealed class OllamaModelNameRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Stream progress lines (pull only).
        /// </summary>
        [JsonPropertyName("stream")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Stream { get; set; } = null;
    }
}

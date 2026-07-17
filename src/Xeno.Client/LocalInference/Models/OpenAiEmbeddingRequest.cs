namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible embeddings request.
    /// </summary>
    public class OpenAiEmbeddingRequest
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

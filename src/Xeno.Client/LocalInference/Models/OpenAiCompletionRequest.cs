namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible completion request.
    /// </summary>
    public class OpenAiCompletionRequest
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Whether streaming was requested.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool? Stream { get; set; } = null;
    }
}

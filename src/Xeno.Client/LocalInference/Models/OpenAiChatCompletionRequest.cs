namespace Xeno.Client.LocalInference.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible chat completion request.
    /// </summary>
    public class OpenAiChatCompletionRequest
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
        public List<OpenAiChatMessage> Messages { get; set; } = new List<OpenAiChatMessage>();

        /// <summary>
        /// Whether streaming was requested.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool? Stream { get; set; } = null;
    }
}

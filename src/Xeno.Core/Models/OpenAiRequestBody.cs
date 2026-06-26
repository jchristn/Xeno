namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Common OpenAI-compatible request fields needed for routing.
    /// </summary>
    public class OpenAiRequestBody
    {
        /// <summary>
        /// Requested model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Whether the caller requested streaming.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool? Stream { get; set; } = null;
    }
}

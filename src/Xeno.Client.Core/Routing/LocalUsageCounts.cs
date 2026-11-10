namespace Xeno.Client.Routing
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The usage object in an OpenAI-compatible response.
    /// </summary>
    public class LocalUsageCounts
    {
        /// <summary>
        /// Prompt tokens, when reported.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public long? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens, when reported.
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public long? CompletionTokens { get; set; } = null;
    }
}

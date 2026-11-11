namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI token breakdown.
    /// </summary>
    public sealed class ChatCompletionTokenDetailsDto
    {
        /// <summary>
        /// Reasoning tokens inside the completion tokens.
        /// </summary>
        [JsonPropertyName("reasoning_tokens")]
        public int? ReasoningTokens { get; set; } = null;

        /// <summary>
        /// Prompt tokens served from the provider's cache.
        /// </summary>
        [JsonPropertyName("cached_tokens")]
        public int? CachedTokens { get; set; } = null;
    }
}

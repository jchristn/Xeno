namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI token usage.
    /// </summary>
    public sealed class ChatCompletionUsageDto
    {
        /// <summary>
        /// Prompt tokens.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Breakdown of completion tokens (reasoning tokens), when the provider reports it.
        /// </summary>
        [JsonPropertyName("completion_tokens_details")]
        public ChatCompletionTokenDetailsDto? CompletionDetails { get; set; } = null;

        /// <summary>
        /// Breakdown of prompt tokens (cached tokens), when the provider reports it.
        /// </summary>
        [JsonPropertyName("prompt_tokens_details")]
        public ChatCompletionTokenDetailsDto? PromptDetails { get; set; } = null;
    }
}

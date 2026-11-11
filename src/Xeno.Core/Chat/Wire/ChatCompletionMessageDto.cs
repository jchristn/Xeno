namespace Xeno.Core.Chat.Wire
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI chat message.
    /// </summary>
    public sealed class ChatCompletionMessageDto
    {
        /// <summary>
        /// "system", "user", "assistant", or "tool".
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        /// <summary>
        /// Text, or null for an assistant message that only calls tools.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// Reasoning text some servers return in non-streaming answers.
        /// </summary>
        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; } = null;

        /// <summary>
        /// Tool calls requested by an assistant message.
        /// </summary>
        [JsonPropertyName("tool_calls")]
        public List<ChatCompletionToolCallDto>? ToolCalls { get; set; } = null;

        /// <summary>
        /// For a tool message, the call it answers.
        /// </summary>
        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; } = null;
    }
}

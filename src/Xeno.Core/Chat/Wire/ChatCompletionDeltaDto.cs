namespace Xeno.Core.Chat.Wire
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Streamed fragment of an OpenAI answer.
    /// </summary>
    public sealed class ChatCompletionDeltaDto
    {
        /// <summary>
        /// Answer text.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// Reasoning text (vLLM, DeepSeek).
        /// </summary>
        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; } = null;

        /// <summary>
        /// Reasoning text (Ollama).
        /// </summary>
        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; } = null;

        /// <summary>
        /// Tool call fragments.
        /// </summary>
        [JsonPropertyName("tool_calls")]
        public List<ChatCompletionToolCallDto>? ToolCalls { get; set; } = null;
    }
}

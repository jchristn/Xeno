namespace Xeno.Core.Chat.Wire
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One streamed OpenAI chunk, or a whole non-streamed answer (choices carry "message" instead of "delta").
    /// </summary>
    public sealed class ChatCompletionChunkDto
    {
        /// <summary>
        /// Choices.
        /// </summary>
        [JsonPropertyName("choices")]
        public List<ChatCompletionChoiceDto>? Choices { get; set; } = null;

        /// <summary>
        /// Token usage, usually on the last chunk.
        /// </summary>
        [JsonPropertyName("usage")]
        public ChatCompletionUsageDto? Usage { get; set; } = null;

        /// <summary>
        /// Error object some servers send inside a stream.
        /// </summary>
        [JsonPropertyName("error")]
        public ChatCompletionErrorDto? Error { get; set; } = null;
    }
}

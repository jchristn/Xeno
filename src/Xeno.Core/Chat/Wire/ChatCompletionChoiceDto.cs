namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One OpenAI choice.
    /// </summary>
    public sealed class ChatCompletionChoiceDto
    {
        /// <summary>
        /// Streamed fragment.
        /// </summary>
        [JsonPropertyName("delta")]
        public ChatCompletionDeltaDto? Delta { get; set; } = null;

        /// <summary>
        /// Whole message in a non-streamed answer.
        /// </summary>
        [JsonPropertyName("message")]
        public ChatCompletionMessageDto? Message { get; set; } = null;

        /// <summary>
        /// Why the model stopped, on the last chunk.
        /// </summary>
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; } = null;
    }
}

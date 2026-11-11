namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI tool call, whole or as a streamed fragment.
    /// </summary>
    public sealed class ChatCompletionToolCallDto
    {
        /// <summary>
        /// Position of the call in a streamed answer.
        /// </summary>
        [JsonPropertyName("index")]
        public int? Index { get; set; } = null;

        /// <summary>
        /// Call identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Always "function".
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; } = "function";

        /// <summary>
        /// Function name and arguments.
        /// </summary>
        [JsonPropertyName("function")]
        public ChatCompletionFunctionCallDto? Function { get; set; } = null;
    }
}

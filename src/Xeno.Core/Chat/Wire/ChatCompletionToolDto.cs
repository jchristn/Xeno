namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A tool offered to the model.
    /// </summary>
    public sealed class ChatCompletionToolDto
    {
        /// <summary>
        /// Always "function".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        /// <summary>
        /// Function definition.
        /// </summary>
        [JsonPropertyName("function")]
        public ChatCompletionFunctionDto Function { get; set; } = new ChatCompletionFunctionDto();
    }
}

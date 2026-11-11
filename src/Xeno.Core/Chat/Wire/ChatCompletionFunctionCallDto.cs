namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Function name and arguments of an OpenAI tool call.
    /// </summary>
    public sealed class ChatCompletionFunctionCallDto
    {
        /// <summary>
        /// Function name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Arguments as JSON text (streamed in fragments).
        /// </summary>
        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; } = null;
    }
}

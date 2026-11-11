namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Error response body: {"error": {...}}.
    /// </summary>
    public sealed class ChatCompletionErrorEnvelopeDto
    {
        /// <summary>
        /// Error.
        /// </summary>
        [JsonPropertyName("error")]
        public ChatCompletionErrorDto? Error { get; set; } = null;
    }
}

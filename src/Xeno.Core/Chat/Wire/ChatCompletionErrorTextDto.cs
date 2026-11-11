namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Error response body whose error is plain text: {"error": "..."}.
    /// </summary>
    public sealed class ChatCompletionErrorTextDto
    {
        /// <summary>
        /// Error text.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;
    }
}

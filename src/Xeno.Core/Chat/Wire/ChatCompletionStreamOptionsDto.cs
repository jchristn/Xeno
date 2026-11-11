namespace Xeno.Core.Chat.Wire
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI stream options.
    /// </summary>
    public sealed class ChatCompletionStreamOptionsDto
    {
        /// <summary>
        /// True to receive a final chunk with token usage.
        /// </summary>
        [JsonPropertyName("include_usage")]
        public bool IncludeUsage { get; set; } = true;
    }
}

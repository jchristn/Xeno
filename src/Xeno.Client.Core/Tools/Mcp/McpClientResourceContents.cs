namespace Xeno.Client.Tools.Mcp
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A resource embedded in a tool result.
    /// </summary>
    internal sealed class McpClientResourceContents
    {
        /// <summary>
        /// Resource URI.
        /// </summary>
        [JsonPropertyName("uri")]
        public string? Uri { get; set; } = null;

        /// <summary>
        /// MIME type.
        /// </summary>
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; } = null;

        /// <summary>
        /// Text contents, for text resources.
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; } = null;

        /// <summary>
        /// Base64 contents, for binary resources.
        /// </summary>
        [JsonPropertyName("blob")]
        public string? Blob { get; set; } = null;
    }
}

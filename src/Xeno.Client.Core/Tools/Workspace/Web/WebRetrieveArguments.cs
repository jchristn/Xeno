namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of web_retrieve.
    /// </summary>
    public sealed class WebRetrieveArguments
    {
        /// <summary>
        /// Absolute HTTP or HTTPS URL.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; } = null;

        /// <summary>
        /// Timeout in milliseconds, or null for 30000.
        /// </summary>
        [JsonPropertyName("timeout_ms")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? TimeoutMs { get; set; } = null;

        /// <summary>
        /// Most characters of text and of HTML to return, or null for 60000.
        /// </summary>
        [JsonPropertyName("max_content_chars")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? MaxContentChars { get; set; } = null;

        /// <summary>
        /// Also return the document's HTML.
        /// </summary>
        [JsonPropertyName("include_html")]
        public bool IncludeHtml { get; set; } = false;
    }
}

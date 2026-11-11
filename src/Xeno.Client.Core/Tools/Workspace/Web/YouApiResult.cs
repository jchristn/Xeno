namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One You.com result.
    /// </summary>
    internal sealed class YouApiResult
    {
        /// <summary>
        /// Title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// URL.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Snippets.
        /// </summary>
        [JsonPropertyName("snippets")]
        public List<string>? Snippets { get; set; } = null;

        /// <summary>
        /// Favicon URL.
        /// </summary>
        [JsonPropertyName("favicon_url")]
        public string? FaviconUrl { get; set; } = null;

        /// <summary>
        /// Thumbnail URL.
        /// </summary>
        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; } = null;

        /// <summary>
        /// Crawled content.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// Crawled Markdown.
        /// </summary>
        [JsonPropertyName("markdown")]
        public string? Markdown { get; set; } = null;

        /// <summary>
        /// Crawled HTML.
        /// </summary>
        [JsonPropertyName("html")]
        public string? Html { get; set; } = null;

        /// <summary>
        /// Page age as given.
        /// </summary>
        [JsonPropertyName("page_age")]
        public string? PageAge { get; set; } = null;
    }
}

namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One search result.
    /// </summary>
    public sealed class WebSearchResultItem
    {
        /// <summary>
        /// Section: web or news.
        /// </summary>
        public string Section { get; set; } = "web";

        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Description, or null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Snippets.
        /// </summary>
        public List<string> Snippets { get; set; } = new List<string>();

        /// <summary>
        /// Provider relevance score, or null.
        /// </summary>
        public double? Score { get; set; } = null;

        /// <summary>
        /// Page content when the provider returned it, or null.
        /// </summary>
        public string? RawContent { get; set; } = null;

        /// <summary>
        /// Favicon URL, or null.
        /// </summary>
        public string? FaviconUrl { get; set; } = null;

        /// <summary>
        /// Thumbnail URL, or null.
        /// </summary>
        public string? ThumbnailUrl { get; set; } = null;

        /// <summary>
        /// Publication date as the provider gave it, or null.
        /// </summary>
        public string? PublishedAt { get; set; } = null;

        /// <summary>
        /// Images for this result.
        /// </summary>
        public List<WebSearchImage> Images { get; set; } = new List<WebSearchImage>();
    }
}

namespace Xeno.Client.Tools.Workspace.Web
{
    using System;

    /// <summary>
    /// Result of web_retrieve, shaped like Mux's.
    /// </summary>
    public sealed class WebRetrieveResult
    {
        /// <summary>
        /// Requested URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// URL after redirects.
        /// </summary>
        public string FinalUrl { get; set; } = String.Empty;

        /// <summary>
        /// Document title, or empty.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int? Status { get; set; } = null;

        /// <summary>
        /// Content-Type header, or null.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Visible text (HTML converted to plain text; other text types as they are).
        /// </summary>
        public string Text { get; set; } = String.Empty;

        /// <summary>
        /// True when the text was cut at max_content_chars or the download limit.
        /// </summary>
        public bool TextTruncated { get; set; } = false;

        /// <summary>
        /// Document HTML when include_html is set, or null.
        /// </summary>
        public string? Html { get; set; } = null;

        /// <summary>
        /// True when the HTML was cut, or null without include_html.
        /// </summary>
        public bool? HtmlTruncated { get; set; } = null;
    }
}

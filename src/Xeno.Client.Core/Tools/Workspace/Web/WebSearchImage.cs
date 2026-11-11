namespace Xeno.Client.Tools.Workspace.Web
{
    using System;

    /// <summary>
    /// An image in search results.
    /// </summary>
    public sealed class WebSearchImage
    {
        /// <summary>
        /// Image URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Description, or null.
        /// </summary>
        public string? Description { get; set; } = null;
    }
}

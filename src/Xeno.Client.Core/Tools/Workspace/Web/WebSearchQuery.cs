namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A normalized search request handed to a provider.
    /// </summary>
    internal sealed class WebSearchQuery
    {
        /// <summary>
        /// Query text.
        /// </summary>
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Most results, 1 to 20.
        /// </summary>
        public int MaxResults { get; set; } = 5;

        /// <summary>
        /// Offset, 0 to 9.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Relative time filter, or null.
        /// </summary>
        public string? Freshness { get; set; } = null;

        /// <summary>
        /// Ask for an answer.
        /// </summary>
        public bool IncludeAnswer { get; set; } = true;

        /// <summary>
        /// Ask for images.
        /// </summary>
        public bool IncludeImages { get; set; } = false;

        /// <summary>
        /// Domains to include.
        /// </summary>
        public List<string> IncludeDomains { get; set; } = new List<string>();

        /// <summary>
        /// Domains to exclude.
        /// </summary>
        public List<string> ExcludeDomains { get; set; } = new List<string>();
    }
}

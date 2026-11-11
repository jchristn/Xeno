namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of web_search, shaped like Mux's web search response.
    /// </summary>
    public sealed class WebSearchResult
    {
        /// <summary>
        /// Provider that answered, for example Tavily or You.com.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Provider type: tavily or you.
        /// </summary>
        public string ProviderType { get; set; } = String.Empty;

        /// <summary>
        /// Query as the provider ran it.
        /// </summary>
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Provider-generated answer, or null.
        /// </summary>
        public string? Answer { get; set; } = null;

        /// <summary>
        /// Provider request id, or null.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Provider-reported latency in seconds, or null.
        /// </summary>
        public double? LatencySeconds { get; set; } = null;

        /// <summary>
        /// Images for the query.
        /// </summary>
        public List<WebSearchImage> Images { get; set; } = new List<WebSearchImage>();

        /// <summary>
        /// Results by section (web, and for You.com news).
        /// </summary>
        public Dictionary<string, List<WebSearchResultItem>> Sections { get; set; } = new Dictionary<string, List<WebSearchResultItem>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whether a fallback provider answered. Xeno uses one provider, so this is always false.
        /// </summary>
        public bool UsedFallback { get; set; } = false;

        /// <summary>
        /// Providers tried, in order.
        /// </summary>
        public List<string> ProvidersTried { get; set; } = new List<string>();

        /// <summary>
        /// Provider attempts.
        /// </summary>
        public List<WebSearchProviderAttempt> Attempts { get; set; } = new List<WebSearchProviderAttempt>();
    }
}

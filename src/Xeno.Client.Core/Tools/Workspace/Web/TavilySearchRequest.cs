namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Body of a Tavily search request (POST https://api.tavily.com/search).
    /// </summary>
    internal sealed class TavilySearchRequest
    {
        /// <summary>
        /// Query.
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Search depth.
        /// </summary>
        [JsonPropertyName("search_depth")]
        public string SearchDepth { get; set; } = "basic";

        /// <summary>
        /// Topic.
        /// </summary>
        [JsonPropertyName("topic")]
        public string Topic { get; set; } = "general";

        /// <summary>
        /// Most results, at most 20.
        /// </summary>
        [JsonPropertyName("max_results")]
        public int MaxResults { get; set; } = 5;

        /// <summary>
        /// Chunks per source.
        /// </summary>
        [JsonPropertyName("chunks_per_source")]
        public int ChunksPerSource { get; set; } = 3;

        /// <summary>
        /// Relative time range, or null.
        /// </summary>
        [JsonPropertyName("time_range")]
        public string? TimeRange { get; set; } = null;

        /// <summary>
        /// Answer mode ("basic"), or null for no answer.
        /// </summary>
        [JsonPropertyName("include_answer")]
        public string? IncludeAnswer { get; set; } = null;

        /// <summary>
        /// Ask for raw page content.
        /// </summary>
        [JsonPropertyName("include_raw_content")]
        public bool IncludeRawContent { get; set; } = false;

        /// <summary>
        /// Ask for images.
        /// </summary>
        [JsonPropertyName("include_images")]
        public bool IncludeImages { get; set; } = false;

        /// <summary>
        /// Ask for image descriptions.
        /// </summary>
        [JsonPropertyName("include_image_descriptions")]
        public bool IncludeImageDescriptions { get; set; } = false;

        /// <summary>
        /// Ask for favicons.
        /// </summary>
        [JsonPropertyName("include_favicon")]
        public bool IncludeFavicon { get; set; } = true;

        /// <summary>
        /// Domains to include, or null.
        /// </summary>
        [JsonPropertyName("include_domains")]
        public List<string>? IncludeDomains { get; set; } = null;

        /// <summary>
        /// Domains to exclude, or null.
        /// </summary>
        [JsonPropertyName("exclude_domains")]
        public List<string>? ExcludeDomains { get; set; } = null;

        /// <summary>
        /// Let Tavily choose parameters.
        /// </summary>
        [JsonPropertyName("auto_parameters")]
        public bool AutoParameters { get; set; } = false;

        /// <summary>
        /// Exact matching.
        /// </summary>
        [JsonPropertyName("exact_match")]
        public bool ExactMatch { get; set; } = false;

        /// <summary>
        /// Ask for usage data.
        /// </summary>
        [JsonPropertyName("include_usage")]
        public bool IncludeUsage { get; set; } = true;

        /// <summary>
        /// Safe search.
        /// </summary>
        [JsonPropertyName("safe_search")]
        public bool SafeSearch { get; set; } = false;
    }
}

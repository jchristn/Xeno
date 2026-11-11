namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of web_search.
    /// </summary>
    public sealed class WebSearchArguments
    {
        /// <summary>
        /// Search query.
        /// </summary>
        [JsonPropertyName("query")]
        public string? Query { get; set; } = null;

        /// <summary>
        /// Most results, or null for the configured number.
        /// </summary>
        [JsonPropertyName("max_results")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? MaxResults { get; set; } = null;

        /// <summary>
        /// Provider to use ("tavily" or "you"), or null for the configured one.
        /// </summary>
        [JsonPropertyName("provider")]
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Relative time filter such as day, week, or month, or null.
        /// </summary>
        [JsonPropertyName("freshness")]
        public string? Freshness { get; set; } = null;

        /// <summary>
        /// Ask for a provider-generated answer; default true.
        /// </summary>
        [JsonPropertyName("include_answer")]
        public bool? IncludeAnswer { get; set; } = null;

        /// <summary>
        /// Ask for images.
        /// </summary>
        [JsonPropertyName("include_images")]
        public bool IncludeImages { get; set; } = false;

        /// <summary>
        /// Result offset for paging, or null.
        /// </summary>
        [JsonPropertyName("offset")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Offset { get; set; } = null;

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
    }
}

namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Body of a You.com search request (POST https://ydc-index.io/v1/search).
    /// </summary>
    internal sealed class YouSearchRequest
    {
        /// <summary>
        /// Query.
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Most results.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; } = 5;

        /// <summary>
        /// Offset, 0 to 9.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Language.
        /// </summary>
        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";

        /// <summary>
        /// Safe search: off, moderate, or strict.
        /// </summary>
        [JsonPropertyName("safesearch")]
        public string SafeSearch { get; set; } = "moderate";

        /// <summary>
        /// Freshness filter, or null.
        /// </summary>
        [JsonPropertyName("freshness")]
        public string? Freshness { get; set; } = null;

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
        /// Crawl timeout in seconds.
        /// </summary>
        [JsonPropertyName("crawl_timeout")]
        public int CrawlTimeout { get; set; } = 15;
    }
}

namespace Xeno.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Paged request history search result.
    /// </summary>
    public class RequestHistorySearchResult
    {
        /// <summary>
        /// Returned request history entries.
        /// </summary>
        [JsonPropertyName("items")]
        public List<RequestHistoryEntry> Items { get; set; } = new List<RequestHistoryEntry>();

        /// <summary>
        /// Total matching request history entries before pagination.
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; } = 0;

        /// <summary>
        /// One-based page number. Defaults to 1.
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of entries returned per page. Defaults to 100 and is capped at 1000.
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 100;
    }
}

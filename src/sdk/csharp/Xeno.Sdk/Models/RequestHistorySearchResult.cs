namespace Xeno.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Xeno paged request history search result DTO.
    /// </summary>
    public class RequestHistorySearchResult
    {
        /// <summary>
        /// Gets or sets the returned request history entries.
        /// </summary>
        [JsonPropertyName("items")]
        public List<RequestHistoryEntry> Items { get; set; } = new List<RequestHistoryEntry>();

        /// <summary>
        /// Gets or sets the total matching request history entries before pagination.
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets the one-based page number.
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; }

        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }
    }
}

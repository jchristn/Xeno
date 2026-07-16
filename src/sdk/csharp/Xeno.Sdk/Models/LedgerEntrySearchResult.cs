namespace Xeno.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger entry search result DTO.
    /// </summary>
    public class LedgerEntrySearchResult
    {
        /// <summary>
        /// Gets or sets returned entries.
        /// </summary>
        [JsonPropertyName("items")]
        public List<LedgerEntryRecord> Items { get; set; } = new List<LedgerEntryRecord>();

        /// <summary>
        /// Gets or sets continuation token for the next page.
        /// </summary>
        [JsonPropertyName("continuationToken")]
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets total matching entries.
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }
    }
}

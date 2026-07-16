namespace Xeno.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger balance search result DTO.
    /// </summary>
    public class LedgerBalanceSearchResult
    {
        /// <summary>
        /// Gets or sets returned balances.
        /// </summary>
        [JsonPropertyName("items")]
        public List<LedgerBalanceResponse> Items { get; set; } = new List<LedgerBalanceResponse>();

        /// <summary>
        /// Gets or sets total matching balances.
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets one-based page number.
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; }

        /// <summary>
        /// Gets or sets page size.
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }
    }
}

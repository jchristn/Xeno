namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger entry search request DTO.
    /// </summary>
    public class LedgerEntrySearchRequest
    {
        /// <summary>
        /// Gets or sets optional tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets optional user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        /// <summary>
        /// Gets or sets optional direction.
        /// </summary>
        [JsonPropertyName("direction")]
        public LedgerEntryDirection? Direction { get; set; }

        /// <summary>
        /// Gets or sets inclusive start timestamp.
        /// </summary>
        [JsonPropertyName("fromUtc")]
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Gets or sets exclusive end timestamp.
        /// </summary>
        [JsonPropertyName("toUtc")]
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Gets or sets optional request history identifier.
        /// </summary>
        [JsonPropertyName("requestHistoryId")]
        public string? RequestHistoryId { get; set; }

        /// <summary>
        /// Gets or sets optional selected client identifier.
        /// </summary>
        [JsonPropertyName("selectedClientId")]
        public string? SelectedClientId { get; set; }

        /// <summary>
        /// Gets or sets continuation token.
        /// </summary>
        [JsonPropertyName("continuationToken")]
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets page size.
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 100;
    }
}

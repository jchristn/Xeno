namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger entry record DTO.
    /// </summary>
    public class LedgerEntryRecord
    {
        /// <summary>
        /// Gets or sets NetLedger entry identifier.
        /// </summary>
        [JsonPropertyName("netLedgerEntryId")]
        public string? NetLedgerEntryId { get; set; }

        /// <summary>
        /// Gets or sets Xeno ledger event identifier.
        /// </summary>
        [JsonPropertyName("ledgerEventId")]
        public string? LedgerEventId { get; set; }

        /// <summary>
        /// Gets or sets Xeno tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets account owner user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets counterparty user identifier.
        /// </summary>
        [JsonPropertyName("counterpartyUserId")]
        public string CounterpartyUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets request history identifier.
        /// </summary>
        [JsonPropertyName("requestHistoryId")]
        public string RequestHistoryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets selected serving client identifier.
        /// </summary>
        [JsonPropertyName("selectedClientId")]
        public string SelectedClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets direction.
        /// </summary>
        [JsonPropertyName("direction")]
        public LedgerEntryDirection Direction { get; set; } = LedgerEntryDirection.Debit;

        /// <summary>
        /// Gets or sets raw token amount.
        /// </summary>
        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        /// <summary>
        /// Gets or sets whether the entry is committed.
        /// </summary>
        [JsonPropertyName("committed")]
        public bool Committed { get; set; }

        /// <summary>
        /// Gets or sets NetLedger labels.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets NetLedger tags.
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets business event timestamp.
        /// </summary>
        [JsonPropertyName("occurredUtc")]
        public DateTime OccurredUtc { get; set; }

        /// <summary>
        /// Gets or sets entry creation timestamp.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }
    }
}

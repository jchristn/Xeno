namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Xeno.Core.Enums;

    /// <summary>
    /// Xeno-shaped ledger entry record.
    /// </summary>
    public class LedgerEntryRecord
    {
        /// <summary>
        /// NetLedger entry identifier.
        /// </summary>
        [JsonPropertyName("netLedgerEntryId")]
        public string? NetLedgerEntryId { get; set; } = null;

        /// <summary>
        /// Xeno ledger event identifier.
        /// </summary>
        [JsonPropertyName("ledgerEventId")]
        public string? LedgerEventId { get; set; } = null;

        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Xeno user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string? XenoUserId { get; set; } = null;

        /// <summary>
        /// Requesting Xeno user identifier.
        /// </summary>
        [JsonPropertyName("requestUserId")]
        public string? RequestUserId { get; set; } = null;

        /// <summary>
        /// Serving Xeno user identifier.
        /// </summary>
        [JsonPropertyName("servingUserId")]
        public string? ServingUserId { get; set; } = null;

        /// <summary>
        /// Counterparty Xeno user identifier.
        /// </summary>
        [JsonPropertyName("counterpartyUserId")]
        public string? CounterpartyUserId { get; set; } = null;

        /// <summary>
        /// Request history identifier.
        /// </summary>
        [JsonPropertyName("requestHistoryId")]
        public string? RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Selected Xeno client identifier.
        /// </summary>
        [JsonPropertyName("selectedClientId")]
        public string? SelectedClientId { get; set; } = null;

        /// <summary>
        /// Entry direction.
        /// </summary>
        [JsonPropertyName("direction")]
        public LedgerEntryDirection Direction { get; set; } = LedgerEntryDirection.Debit;

        /// <summary>
        /// Raw token amount.
        /// </summary>
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; } = 0m;

        /// <summary>
        /// Whether the entry is committed in NetLedger.
        /// </summary>
        [JsonPropertyName("committed")]
        public bool IsCommitted { get; set; } = false;

        /// <summary>
        /// NetLedger labels.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// NetLedger tags.
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Business event timestamp.
        /// </summary>
        [JsonPropertyName("occurredUtc")]
        public DateTime? OccurredUtc { get; set; } = null;

        /// <summary>
        /// NetLedger creation timestamp.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; } = null;
    }
}

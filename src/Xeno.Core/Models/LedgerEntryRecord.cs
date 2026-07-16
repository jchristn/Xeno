namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Xeno-shaped ledger entry record.
    /// </summary>
    public class LedgerEntryRecord
    {
        /// <summary>
        /// NetLedger entry identifier.
        /// </summary>
        public string? NetLedgerEntryId { get; set; } = null;

        /// <summary>
        /// Xeno ledger event identifier.
        /// </summary>
        public string? LedgerEventId { get; set; } = null;

        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Xeno user identifier.
        /// </summary>
        public string? XenoUserId { get; set; } = null;

        /// <summary>
        /// Request history identifier.
        /// </summary>
        public string? RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Selected Xeno client identifier.
        /// </summary>
        public string? SelectedClientId { get; set; } = null;

        /// <summary>
        /// Entry direction.
        /// </summary>
        public LedgerEntryDirection Direction { get; set; } = LedgerEntryDirection.Debit;

        /// <summary>
        /// Raw token amount.
        /// </summary>
        public decimal Amount { get; set; } = 0m;

        /// <summary>
        /// Whether the entry is committed in NetLedger.
        /// </summary>
        public bool IsCommitted { get; set; } = false;

        /// <summary>
        /// NetLedger labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// NetLedger tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Business event timestamp.
        /// </summary>
        public DateTime? OccurredUtc { get; set; } = null;

        /// <summary>
        /// NetLedger creation timestamp.
        /// </summary>
        public DateTime? CreatedUtc { get; set; } = null;
    }
}

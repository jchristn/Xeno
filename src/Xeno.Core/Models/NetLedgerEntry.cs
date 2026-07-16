namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// NetLedger entry contract.
    /// </summary>
    public class NetLedgerEntry
    {
        /// <summary>
        /// Entry identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = String.Empty;

        /// <summary>
        /// Entry type.
        /// </summary>
        public string Type { get; set; } = String.Empty;

        /// <summary>
        /// Entry amount.
        /// </summary>
        public decimal Amount { get; set; } = 0m;

        /// <summary>
        /// Entry description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Whether the entry is committed.
        /// </summary>
        public bool IsCommitted { get; set; } = false;

        /// <summary>
        /// Entry labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Entry tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}

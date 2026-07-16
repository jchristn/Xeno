namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// NetLedger create entry request.
    /// </summary>
    public class NetLedgerCreateEntryRequest
    {
        /// <summary>
        /// Entry amount.
        /// </summary>
        public decimal Amount { get; set; } = 0m;

        /// <summary>
        /// Entry notes.
        /// </summary>
        public string? Notes { get; set; } = null;

        /// <summary>
        /// Whether the entry should be committed immediately.
        /// </summary>
        public bool IsCommitted { get; set; } = true;

        /// <summary>
        /// Entry labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Entry tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

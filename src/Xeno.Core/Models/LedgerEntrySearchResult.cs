namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ledger entry search result.
    /// </summary>
    public class LedgerEntrySearchResult
    {
        /// <summary>
        /// Result items.
        /// </summary>
        public List<LedgerEntryRecord> Items { get; set; } = new List<LedgerEntryRecord>();

        /// <summary>
        /// Continuation token, when more entries are available.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Whether no more results are available.
        /// </summary>
        public bool EndOfResults { get; set; } = true;
    }
}

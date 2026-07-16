namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ledger balance search result.
    /// </summary>
    public class LedgerBalanceSearchResult
    {
        /// <summary>
        /// Result items.
        /// </summary>
        public List<LedgerBalanceResponse> Items { get; set; } = new List<LedgerBalanceResponse>();

        /// <summary>
        /// Total result count.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Page number.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 100;
    }
}

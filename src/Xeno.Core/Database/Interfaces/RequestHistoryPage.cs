namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using Xeno.Core.Models;

    /// <summary>
    /// A page of request history entries with the total matching count.
    /// </summary>
    public class RequestHistoryPage
    {
        /// <summary>
        /// Entries in this page. Fully hydrated when the query requested bodies; otherwise
        /// only promoted columns are populated.
        /// </summary>
        public List<RequestHistoryEntry> Items { get; set; } = new List<RequestHistoryEntry>();

        /// <summary>
        /// Total matching entries before pagination.
        /// </summary>
        public int Total { get; set; } = 0;
    }
}

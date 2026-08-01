namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using Xeno.Core.Models;

    /// <summary>
    /// A page of audit records plus the total count matching the query.
    /// </summary>
    public class AuditRecordPage
    {
        /// <summary>
        /// Matching audit records for the requested page.
        /// </summary>
        public List<AuditRecord> Items { get; set; } = new List<AuditRecord>();

        /// <summary>
        /// Total number of records matching the query, ignoring paging.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuditRecordPage()
        {
        }
    }
}

namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Local request history list response.
    /// </summary>
    public class LocalRequestHistoryListResponse
    {
        /// <summary>
        /// Request history entries.
        /// </summary>
        public List<LocalRequestHistoryEntry> Entries { get; set; } = new List<LocalRequestHistoryEntry>();
    }
}

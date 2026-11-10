namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;
    using Xeno.Client.Routing;

    /// <summary>
    /// One page of local inference listener requests, newest first.
    /// </summary>
    public class LocalInferenceRequestPage
    {
        /// <summary>
        /// Records. Never null.
        /// </summary>
        public List<LocalInferenceRequestRecord> Items { get; set; } = new List<LocalInferenceRequestRecord>();

        /// <summary>
        /// Total stored records.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// One-based page number.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 25;
    }
}

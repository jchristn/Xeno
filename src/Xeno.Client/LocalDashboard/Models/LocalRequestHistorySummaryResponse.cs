namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Local request history summary response.
    /// </summary>
    public class LocalRequestHistorySummaryResponse
    {
        /// <summary>
        /// Summary buckets.
        /// </summary>
        public List<LocalRequestHistorySummaryBucket> Buckets { get; set; } = new List<LocalRequestHistorySummaryBucket>();
    }
}

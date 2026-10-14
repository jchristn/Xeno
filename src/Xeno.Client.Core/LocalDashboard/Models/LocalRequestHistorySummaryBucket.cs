namespace Xeno.Client.LocalDashboard.Models
{
    using System;

    /// <summary>
    /// Local request history summary bucket.
    /// </summary>
    public class LocalRequestHistorySummaryBucket
    {
        /// <summary>
        /// Bucket start timestamp.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Successful request count.
        /// </summary>
        public int Success { get; set; } = 0;

        /// <summary>
        /// Failed request count.
        /// </summary>
        public int Failure { get; set; } = 0;

        /// <summary>
        /// Total request count.
        /// </summary>
        public int Total { get; set; } = 0;
    }
}

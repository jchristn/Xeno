namespace Xeno.Client.LocalDashboard.Models
{
    using System;

    /// <summary>
    /// Local dashboard statistics response.
    /// </summary>
    public class LocalDashboardStatisticsResponse
    {
        /// <summary>
        /// Total request count.
        /// </summary>
        public int Requests { get; set; } = 0;

        /// <summary>
        /// Failed request count.
        /// </summary>
        public int Failures { get; set; } = 0;

        /// <summary>
        /// Streaming request count.
        /// </summary>
        public int StreamingRequests { get; set; } = 0;

        /// <summary>
        /// Average request latency.
        /// </summary>
        public double? AverageLatencyMs { get; set; } = null;

        /// <summary>
        /// Oldest request timestamp.
        /// </summary>
        public DateTime? OldestRequestUtc { get; set; } = null;

        /// <summary>
        /// Newest request timestamp.
        /// </summary>
        public DateTime? NewestRequestUtc { get; set; } = null;
    }
}

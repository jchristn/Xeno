namespace Xeno.Tray
{
    using System;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// Tray-facing local request statistics.
    /// </summary>
    public sealed class XenoTrayStatistics
    {
        /// <summary>
        /// Total requests.
        /// </summary>
        public int Requests { get; set; } = 0;

        /// <summary>
        /// Failed requests.
        /// </summary>
        public int Failures { get; set; } = 0;

        /// <summary>
        /// Streaming requests.
        /// </summary>
        public int StreamingRequests { get; set; } = 0;

        /// <summary>
        /// Average latency in milliseconds.
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

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Build tray statistics from local dashboard statistics.
        /// </summary>
        /// <param name="statistics">Local dashboard statistics.</param>
        /// <returns>Tray statistics.</returns>
        public static XenoTrayStatistics From(LocalDashboardStatisticsResponse statistics)
        {
            return new XenoTrayStatistics
            {
                Requests = statistics.Requests,
                Failures = statistics.Failures,
                StreamingRequests = statistics.StreamingRequests,
                AverageLatencyMs = statistics.AverageLatencyMs,
                OldestRequestUtc = statistics.OldestRequestUtc,
                NewestRequestUtc = statistics.NewestRequestUtc
            };
        }
    }
}

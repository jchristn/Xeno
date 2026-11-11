namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One bucket of the request history summary.
    /// </summary>
    public sealed class RequestHistorySummaryBucketResponse
    {
        /// <summary>
        /// Bucket start, UTC.
        /// </summary>
        [JsonPropertyName("startUtc")]
        public DateTime StartUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Bucket end, UTC.
        /// </summary>
        [JsonPropertyName("endUtc")]
        public DateTime EndUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Requests in the bucket.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; } = 0;

        /// <summary>
        /// Requests with status 400 or above.
        /// </summary>
        [JsonPropertyName("errors")]
        public int Errors { get; set; } = 0;

        /// <summary>
        /// Requests below status 400.
        /// </summary>
        [JsonPropertyName("success")]
        public int Success { get; set; } = 0;

        /// <summary>
        /// Same as Errors (kept for older dashboards).
        /// </summary>
        [JsonPropertyName("failed")]
        public int Failed { get; set; } = 0;
    }
}
